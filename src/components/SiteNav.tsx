"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { LogoIcon } from "./icons";

const LINKS = [
  { href: "/", label: "Investigate", hint: "Test any variable, anywhere" },
  { href: "/inferences", label: "Inferences", hint: "What the Phoenix records show" },
  { href: "/what-if", label: "What if", hint: "Change the climate and see what follows" },
] as const;

/** The site-wide navigation bar: brand on the left, the three sections on the right. */
export function SiteNav() {
  const pathname = usePathname();
  return (
    <nav aria-label="Main" className="mx-auto max-w-[1180px] px-4 pt-4 sm:px-6">
      <div className="flex flex-wrap items-center justify-between gap-x-6 gap-y-3">
        <Link href="/" className="flex items-center gap-2">
          <LogoIcon className="h-9 w-9" />
          <span className="display text-2xl sm:text-3xl">Trend Detective</span>
        </Link>

        <ul className="seg" role="list">
          {LINKS.map((link) => {
            const active = link.href === "/" ? pathname === "/" : pathname.startsWith(link.href);
            return (
              <li key={link.href} className="navlink-item">
                <Link
                  href={link.href}
                  title={link.hint}
                  aria-current={active ? "page" : undefined}
                  className="navlink"
                >
                  {link.label}
                </Link>
              </li>
            );
          })}
        </ul>
      </div>
    </nav>
  );
}
