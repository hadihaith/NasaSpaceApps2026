"use client";

import { MedalIcon } from "./icons";

export interface Toast {
  id: number;
  title: string;
  body: string;
}

/** Badge and promotion announcements, bottom right. */
export function Toasts({ toasts }: { toasts: Toast[] }) {
  return (
    <div
      aria-live="polite"
      className="pointer-events-none fixed right-4 bottom-4 z-50 flex w-[min(360px,calc(100vw-32px))] flex-col gap-3"
    >
      {toasts.map((t) => (
        <div key={t.id} className="toast card flex items-start gap-3 bg-gold p-4 text-[#10242e]">
          <MedalIcon className="h-8 w-8 shrink-0" />
          <div>
            <p className="font-bold">{t.title}</p>
            <p className="text-sm text-[#3b4f57]">{t.body}</p>
          </div>
        </div>
      ))}
    </div>
  );
}
