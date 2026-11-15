"use client";

import { useEffect, useRef, useState } from "react";

/** True when the visitor has asked their system for less motion. */
export function prefersReducedMotion(): boolean {
  return typeof window !== "undefined" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

/** The content width of an element, kept current as it resizes. */
export function useWidth<T extends HTMLElement>(fallback = 640) {
  const ref = useRef<T>(null);
  const [width, setWidth] = useState(fallback);
  useEffect(() => {
    if (!ref.current) return;
    const observer = new ResizeObserver(([entry]) => setWidth(Math.max(220, entry.contentRect.width)));
    observer.observe(ref.current);
    return () => observer.disconnect();
  }, []);
  return [ref, width] as const;
}

/** A number that counts up from zero to `target`, eased, after `delay` ms. */
export function useCountUp(target: number, duration = 1100, delay = 0): number {
  const [value, setValue] = useState(() => (prefersReducedMotion() ? target : 0));
  useEffect(() => {
    if (!Number.isFinite(target)) return;
    let frame = 0;
    if (prefersReducedMotion()) {
      frame = requestAnimationFrame(() => setValue(target));
      return () => cancelAnimationFrame(frame);
    }
    let start = 0;
    const tick = (now: number) => {
      if (!start) start = now + delay;
      const k = Math.min(Math.max((now - start) / duration, 0), 1);
      setValue(target * (1 - (1 - k) ** 3));
      if (k < 1) frame = requestAnimationFrame(tick);
    };
    frame = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frame);
  }, [target, duration, delay]);
  return Number.isFinite(target) ? value : target;
}

export function CountUp({
  value,
  format,
  duration,
  delay,
}: {
  value: number;
  format: (v: number) => string;
  duration?: number;
  delay?: number;
}) {
  const shown = useCountUp(value, duration, delay);
  return <>{format(shown)}</>;
}
