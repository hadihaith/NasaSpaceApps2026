"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { HOW_TO, SECTIONS, sectionIndex } from "@/lib/site";

/**
 * The site-wide navigation bar, styled to match the home screen (the game's dark
 * bridge palette and insignia). Sections are numbered in the order to visit them.
 * Keep aria-label="Main", because the game's fullscreen lab hides this bar by that label.
 */
export function SiteNav() {
  const pathname = usePathname();
  const current = sectionIndex(pathname);
  const onHowTo = pathname.startsWith(HOW_TO.href);

  return (
    <nav aria-label="Main" className="site-nav">
      <div className="site-nav-inner">
        <Link href="/" className="site-nav-brand">
          <span className="site-nav-insignia" aria-hidden>
            <i />
            <b>✦</b>
          </span>
          <span>
            <strong>The Great Homecoming</strong>
            <small>NASA Space Apps 2026</small>
          </span>
        </Link>

        <ol className="site-nav-steps" aria-label="Sections, in order">
          {SECTIONS.map((section, i) => (
            <li key={section.href}>
              <Link href={section.href} title={section.summary} aria-current={i === current ? "page" : undefined}>
                <span className="site-nav-num" aria-hidden>
                  {i + 1}
                </span>
                {section.label}
              </Link>
            </li>
          ))}
        </ol>

        <Link
          href={HOW_TO.href}
          className="site-nav-help"
          aria-current={onHowTo ? "page" : undefined}
          data-new={pathname === "/" ? "" : undefined}
        >
          <span aria-hidden>?</span>
          {pathname === "/" ? "New here? How to" : HOW_TO.label}
        </Link>
      </div>
    </nav>
  );
}
