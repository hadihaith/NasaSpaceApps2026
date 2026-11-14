import Link from "next/link";
import { HOW_TO, KIND_LABEL, SECTIONS } from "@/lib/site";

/**
 * Ends a section page by pointing to the next one in the site's order, so a visitor
 * always knows where to go. After the last section it loops back to the story.
 */
export function NextStep({ from }: { from: (typeof SECTIONS)[number]["href"] | typeof HOW_TO.href }) {
  const index = SECTIONS.findIndex((s) => s.href === from);
  const nextIndex = index < 0 ? 0 : (index + 1) % SECTIONS.length;
  const next = SECTIONS[nextIndex];
  const loopsBack = index === SECTIONS.length - 1;

  return (
    <aside aria-labelledby="next-step-heading" className="card mt-16 p-5 sm:p-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div className="min-w-0">
          <p className="text-sm font-bold text-ink-2" id="next-step-heading">
            {index < 0 ? "Start here" : loopsBack ? "You've seen every section. Back to the start:" : "Next step"}
          </p>
          <p className="display mt-1 text-3xl">
            <span className="tabular">{nextIndex + 1}.</span> {next.title}
          </p>
          <p className="mt-1 max-w-[60ch] text-ink-2">
            {next.summary} <span className="chip ml-1 align-middle">{KIND_LABEL[next.kind]}</span>
          </p>
        </div>
        <div className="flex flex-wrap gap-3">
          <Link href={next.href} className="btn btn-primary">
            Go to {next.label} <span aria-hidden>→</span>
          </Link>
          {from !== HOW_TO.href && (
            <Link href={HOW_TO.href} className="btn">
              How to use the site
            </Link>
          )}
        </div>
      </div>
    </aside>
  );
}
