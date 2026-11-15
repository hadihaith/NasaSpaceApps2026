"use client";

import Link from "next/link";
import {
  useEffect,
  useId,
  useRef,
  useState,
  useSyncExternalStore,
  type FormEvent,
  type KeyboardEvent,
  type ReactNode,
} from "react";

interface Message {
  role: "user" | "assistant";
  text: string;
}

interface Stored {
  open: boolean;
  messages: Message[];
}

const STORE_KEY = "app-assistant-v1";
const EMPTY: Stored = { open: false, messages: [] };

// The conversation lives in a tiny store mirrored to sessionStorage, so it survives page
// changes within a tab. The server always renders the empty state, so hydration matches.
let memory: Stored | null = null;
const listeners = new Set<() => void>();

function readStore(): Stored {
  if (memory) return memory;
  try {
    const saved = JSON.parse(sessionStorage.getItem(STORE_KEY) ?? "null") as Partial<Stored> | null;
    memory = {
      open: Boolean(saved?.open),
      messages: Array.isArray(saved?.messages) ? saved.messages.slice(-30) : [],
    };
  } catch {
    memory = EMPTY;
  }
  return memory;
}

function writeStore(next: Stored) {
  memory = next;
  try {
    sessionStorage.setItem(STORE_KEY, JSON.stringify({ open: next.open, messages: next.messages.slice(-30) }));
  } catch {
    /* Storage unavailable: keep the conversation in memory only. */
  }
  listeners.forEach((listener) => listener());
}

function subscribeStore(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
const MAX_CHARS = 1500;
const STARTERS = [
  "What can I do in this app?",
  "How do I unlock the game's ending?",
  "Is Phoenix really getting hotter?",
  "What does “statistically significant” mean here?",
];

const PAGE_PATHS = ["/inferences", "/what-if", "/detective", "/challenge", "/how-to"];

/**
 * Render the assistant's text safely as React nodes (never as HTML): page paths become
 * links, **bold** and *italic* become emphasis, and stray Markdown backticks are dropped.
 */
function renderText(text: string): ReactNode[] {
  const clean = text.replace(/`([^`]+)`/g, "$1").replace(/^\s*\*\s+/gm, "- ");
  const parts = clean.split(/(\*\*[^*\n]+\*\*|\*[^*\n]+\*|\/inferences\b|\/what-if\b|\/detective\b|\/challenge\b|\/how-to\b)/g);
  return parts.map((part, i) => {
    if (PAGE_PATHS.includes(part)) {
      return (
        <Link key={i} href={part} className="font-bold underline underline-offset-2">
          {part}
        </Link>
      );
    }
    if (part.startsWith("**") && part.endsWith("**") && part.length > 4) return <strong key={i}>{part.slice(2, -2)}</strong>;
    if (part.startsWith("*") && part.endsWith("*") && part.length > 2) return <em key={i}>{part.slice(1, -1)}</em>;
    return <span key={i}>{part}</span>;
  });
}

function ChatIcon(props: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.2} strokeLinecap="round" strokeLinejoin="round" aria-hidden {...props}>
      <path d="M4 5h16v11H9l-5 4z" />
      <path d="M8 9.5h8M8 12.5h5" />
    </svg>
  );
}

/** A fixed, collapsible assistant in the corner of every page. */
export function ChatWidget({ enabled }: { enabled: boolean }) {
  const { open, messages } = useSyncExternalStore(subscribeStore, readStore, () => EMPTY);
  const setOpen = (value: boolean) => writeStore({ ...readStore(), open: value });
  const setMessages = (next: Message[] | ((prev: Message[]) => Message[])) =>
    writeStore({ ...readStore(), messages: typeof next === "function" ? next(readStore().messages) : next });
  const [draft, setDraft] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const panelId = useId();
  const listRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const launcherRef = useRef<HTMLButtonElement>(null);
  const abortRef = useRef<AbortController | null>(null);

  useEffect(() => {
    listRef.current?.scrollTo({ top: listRef.current.scrollHeight });
  }, [messages, open]);

  useEffect(() => {
    if (open) inputRef.current?.focus();
  }, [open]);

  function close() {
    setOpen(false);
    launcherRef.current?.focus();
  }

  async function send(text: string) {
    const question = text.trim().slice(0, MAX_CHARS);
    if (!question || busy || !enabled) return;
    const history: Message[] = [...messages, { role: "user", text: question }];
    setMessages([...history, { role: "assistant", text: "" }]);
    setDraft("");
    setError(null);
    setBusy(true);

    const controller = new AbortController();
    abortRef.current = controller;
    try {
      const res = await fetch("/api/chat", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ messages: history }),
        signal: controller.signal,
      });
      if (!res.ok || !res.body) {
        const body = await res.json().catch(() => null);
        throw new Error(body?.error ?? `The assistant returned an error (${res.status}).`);
      }
      const reader = res.body.getReader();
      const decoder = new TextDecoder();
      let answer = "";
      for (;;) {
        const { done, value } = await reader.read();
        if (done) break;
        answer += decoder.decode(value, { stream: true });
        setMessages([...history, { role: "assistant", text: answer }]);
      }
    } catch (e) {
      if (controller.signal.aborted) {
        setMessages((prev) => {
          const last = prev[prev.length - 1];
          return last?.role === "assistant" && !last.text ? prev.slice(0, -1) : prev;
        });
      } else {
        setMessages(history);
        setError(e instanceof Error ? e.message : "Something went wrong.");
      }
    } finally {
      setBusy(false);
      abortRef.current = null;
    }
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    void send(draft);
  }

  function onKeyDown(e: KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) {
      e.preventDefault();
      void send(draft);
    }
  }

  return (
    <div
      className="chat-widget fixed right-4 bottom-4 z-[60] flex flex-col items-end gap-3 sm:right-6 sm:bottom-6"
      onKeyDown={(e) => {
        if (e.key === "Escape" && open) close();
      }}
    >
      {open && (
        <section
          id={panelId}
          aria-label="App assistant"
          className="flex h-[min(560px,calc(100dvh-7rem))] w-[min(380px,calc(100vw-2rem))] flex-col overflow-hidden rounded-2xl border-[2.5px] border-edge bg-surface text-ink shadow-[5px_5px_0_var(--drop)]"
        >
          <header className="flex items-start justify-between gap-3 border-b-2 border-line px-4 py-3">
            <div>
              <h2 className="display text-xl">App assistant</h2>
              <p className="text-xs text-ink-2">AI help with the app, its data and its science. It can be wrong.</p>
            </div>
            <div className="flex shrink-0 gap-1">
              {messages.length > 0 && (
                <button
                  type="button"
                  onClick={() => {
                    abortRef.current?.abort();
                    setMessages([]);
                    setError(null);
                  }}
                  className="rounded-lg px-2 py-1 text-xs font-bold text-ink-2 hover:bg-wash"
                >
                  Clear
                </button>
              )}
              <button
                type="button"
                onClick={close}
                aria-label="Collapse the assistant"
                className="grid h-8 w-8 place-items-center rounded-lg text-lg font-bold hover:bg-wash"
              >
                <span aria-hidden>–</span>
              </button>
            </div>
          </header>

          <div ref={listRef} className="flex-1 space-y-3 overflow-y-auto px-4 py-3" aria-live="polite">
            {!enabled ? (
              <p className="text-sm text-ink-2">
                The assistant isn&apos;t switched on for this site yet. It needs a Gemini API key on the server.
              </p>
            ) : messages.length === 0 ? (
              <div>
                <p className="text-sm text-ink-2">Ask anything about the app. For example:</p>
                <ul className="mt-3 flex flex-col items-start gap-2">
                  {STARTERS.map((s) => (
                    <li key={s}>
                      <button
                        type="button"
                        onClick={() => void send(s)}
                        className="rounded-xl border-2 border-line px-3 py-1.5 text-left text-sm hover:border-edge"
                      >
                        {s}
                      </button>
                    </li>
                  ))}
                </ul>
              </div>
            ) : (
              messages.map((m, i) =>
                m.role === "user" ? (
                  <p
                    key={i}
                    className="ml-8 rounded-2xl rounded-br-md bg-gold px-3 py-2 text-sm whitespace-pre-wrap text-[#10242e]"
                  >
                    {m.text}
                  </p>
                ) : (
                  <div key={i} className="mr-6 rounded-2xl rounded-bl-md bg-surface-2 px-3 py-2 text-sm whitespace-pre-wrap">
                    {m.text ? renderText(m.text) : <span className="text-ink-2">Thinking…</span>}
                  </div>
                ),
              )
            )}
            {error && <p className="text-sm text-miss">{error}</p>}
          </div>

          <form onSubmit={onSubmit} className="flex items-end gap-2 border-t-2 border-line p-3">
            <label htmlFor={`${panelId}-input`} className="sr-only">
              Your question
            </label>
            <textarea
              id={`${panelId}-input`}
              ref={inputRef}
              rows={2}
              value={draft}
              maxLength={MAX_CHARS}
              disabled={!enabled}
              onChange={(e) => setDraft(e.target.value)}
              onKeyDown={onKeyDown}
              placeholder={enabled ? "Ask about the app…" : "Assistant unavailable"}
              className="min-h-[2.75rem] flex-1 resize-none rounded-xl border-2 border-line bg-surface px-3 py-2 text-sm text-ink focus:border-edge focus:outline-none"
            />
            {busy ? (
              <button type="button" className="btn px-3 py-2 text-sm" onClick={() => abortRef.current?.abort()}>
                Stop
              </button>
            ) : (
              <button type="submit" className="btn btn-primary px-3 py-2 text-sm" disabled={!enabled || !draft.trim()}>
                Send
              </button>
            )}
          </form>
        </section>
      )}

      <button
        ref={launcherRef}
        type="button"
        aria-expanded={open}
        aria-controls={open ? panelId : undefined}
        onClick={() => (open ? close() : setOpen(true))}
        className="btn btn-primary h-14 gap-2 px-5 text-base"
      >
        <ChatIcon className="h-6 w-6" />
        {open ? "Close" : "Ask the app"}
      </button>
    </div>
  );
}
