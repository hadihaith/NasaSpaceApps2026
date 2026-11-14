"use client";

import { useSyncExternalStore } from "react";
import { NEW_PROGRESS, type Progress } from "./game";

/**
 * The player's progress, kept in this browser's local storage. Storage can be
 * missing or blocked (private windows, strict settings), so every access is
 * guarded and the game still runs, it just forgets on reload.
 */
const KEY = "trend-detective/progress/v1";

let cache: Progress | null = null;
const listeners = new Set<() => void>();

function read(): Progress {
  if (cache) return cache;
  cache = NEW_PROGRESS;
  try {
    const raw = window.localStorage.getItem(KEY);
    if (raw) cache = sanitize(JSON.parse(raw));
  } catch {
    // Unreadable storage: start fresh.
  }
  return cache;
}

function sanitize(value: unknown): Progress {
  const v = (value ?? {}) as Partial<Progress>;
  const num = (x: unknown) => (typeof x === "number" && Number.isFinite(x) && x >= 0 ? x : 0);
  const list = <T,>(x: unknown) => (Array.isArray(x) ? (x as T[]) : []);
  return {
    xp: num(v.xp),
    streak: num(v.streak),
    bestStreak: num(v.bestStreak),
    calls: num(v.calls),
    spotOn: num(v.spotOn),
    caseStars: v.caseStars && typeof v.caseStars === "object" ? v.caseStars : {},
    badges: list(v.badges),
    themes: list(v.themes),
    cells: list(v.cells),
  };
}

function write(progress: Progress) {
  cache = progress;
  try {
    window.localStorage.setItem(KEY, JSON.stringify(progress));
  } catch {
    // Storage full or blocked: keep playing from memory.
  }
  for (const listener of listeners) listener();
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function useProgress(): Progress {
  return useSyncExternalStore(subscribe, read, () => NEW_PROGRESS);
}

export const saveProgress = write;

export function resetProgress() {
  write(NEW_PROGRESS);
}
