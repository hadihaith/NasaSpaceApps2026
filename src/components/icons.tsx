import type { SVGProps } from "react";
import type { ThemeId } from "@/lib/catalog";
import type { Guess, Verdict } from "@/lib/game";

type IconProps = SVGProps<SVGSVGElement>;

const stroke = {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: 2.2,
  strokeLinecap: "round" as const,
  strokeLinejoin: "round" as const,
  "aria-hidden": true,
};

export function ThermometerIcon(props: IconProps) {
  return (
    <svg {...stroke} {...props}>
      <path d="M10 13.5V5a2 2 0 1 1 4 0v8.5a4 4 0 1 1-4 0Z" />
      <path d="M12 9v6.5" />
    </svg>
  );
}

export function DropIcon(props: IconProps) {
  return (
    <svg {...stroke} {...props}>
      <path d="M12 3c3.2 4.4 6 7.6 6 11a6 6 0 0 1-12 0c0-3.4 2.8-6.6 6-11Z" />
    </svg>
  );
}

export function WindIcon(props: IconProps) {
  return (
    <svg {...stroke} {...props}>
      <path d="M3 8h10a3 3 0 1 0-3-3" />
      <path d="M3 12h16" />
      <path d="M3 16h12a3 3 0 1 1-3 3" />
    </svg>
  );
}

export function SunIcon(props: IconProps) {
  return (
    <svg {...stroke} {...props}>
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2.5v2M12 19.5v2M2.5 12h2M19.5 12h2M5.3 5.3l1.4 1.4M17.3 17.3l1.4 1.4M5.3 18.7l1.4-1.4M17.3 6.7l1.4-1.4" />
    </svg>
  );
}

export function ThemeIcon({ theme, ...props }: IconProps & { theme: ThemeId }) {
  if (theme === "heat") return <ThermometerIcon {...props} />;
  if (theme === "water") return <DropIcon {...props} />;
  if (theme === "air") return <WindIcon {...props} />;
  return <SunIcon {...props} />;
}

export function GuessIcon({ guess, ...props }: IconProps & { guess: Guess }) {
  if (guess === "rising") {
    return (
      <svg {...stroke} {...props}>
        <path d="M3 17l6-6 4 4 8-8" />
        <path d="M15 7h6v6" />
      </svg>
    );
  }
  if (guess === "falling") {
    return (
      <svg {...stroke} {...props}>
        <path d="M3 7l6 6 4-4 8 8" />
        <path d="M15 17h6v-6" />
      </svg>
    );
  }
  return (
    <svg {...stroke} {...props}>
      <path d="M3 12c2.2-3 4.3 3 6.5 0s4.3 3 6.5 0" />
      <path d="M18.5 9l3 3-3 3" />
    </svg>
  );
}

export function VerdictIcon({ verdict, ...props }: IconProps & { verdict: Verdict }) {
  if (verdict === "spot-on") {
    return (
      <svg {...stroke} strokeWidth={3} {...props}>
        <path d="M5 12.5l4.5 4.5L19 7.5" />
      </svg>
    );
  }
  if (verdict === "close") {
    return (
      <svg {...stroke} strokeWidth={3} {...props}>
        <path d="M4 13c2.5-3 5-3 8 0s5.5 3 8 0" />
      </svg>
    );
  }
  return (
    <svg {...stroke} strokeWidth={3} {...props}>
      <path d="M6 6l12 12M18 6 6 18" />
    </svg>
  );
}

export function StarIcon({ filled, ...props }: IconProps & { filled: boolean }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill={filled ? "var(--star, var(--gold))" : "none"}
      stroke="var(--edge)"
      strokeWidth={1.8}
      strokeLinejoin="round"
      aria-hidden
      {...props}
    >
      <path d="M12 2.8l2.8 5.9 6.4.8-4.7 4.4 1.2 6.4L12 17.2l-5.7 3.1 1.2-6.4-4.7-4.4 6.4-.8Z" />
    </svg>
  );
}

export function FlameIcon(props: IconProps) {
  return (
    <svg {...stroke} {...props}>
      <path d="M12 3c1 3.5 5 5.5 5 10a5 5 0 0 1-10 0c0-2.5 1.5-4 2.5-5 .3 1.6 1 2.5 2 3 .2-3-.5-5.5.5-8Z" />
    </svg>
  );
}

export function MedalIcon(props: IconProps) {
  return (
    <svg {...stroke} {...props}>
      <path d="M8.5 11 6 3h4l2 5 2-5h4l-2.5 8" />
      <circle cx="12" cy="15.5" r="5" />
    </svg>
  );
}

export function LockIcon(props: IconProps) {
  return (
    <svg {...stroke} {...props}>
      <rect x="5" y="11" width="14" height="10" rx="2" />
      <path d="M8 11V8a4 4 0 1 1 8 0v3" />
    </svg>
  );
}

/** A magnifying glass over a globe. */
export function LogoIcon(props: IconProps) {
  return (
    <svg viewBox="0 0 32 32" fill="none" aria-hidden {...props}>
      <circle cx="13" cy="13" r="9.5" fill="var(--series-a)" stroke="var(--edge)" strokeWidth="2.5" />
      <path
        d="M3.8 13h18.4M13 3.5c-3 3-3 16 0 19M13 3.5c3 3 3 16 0 19"
        stroke="#fff"
        strokeWidth="1.6"
        opacity="0.8"
      />
      <path d="M20 20l8.5 8.5" stroke="var(--edge)" strokeWidth="4.5" strokeLinecap="round" />
    </svg>
  );
}
