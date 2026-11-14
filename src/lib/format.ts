/** Number formatting shared by the verdict, the evidence table and the chart. */

const MINUS = "−";

/** Round to a number of significant figures, without scientific notation. */
export function sig(x: number, figures = 2): string {
  if (!Number.isFinite(x)) return "n/a";
  if (x === 0) return "0";
  const magnitude = Math.floor(Math.log10(Math.abs(x)));
  const decimals = Math.max(0, Math.min(8, figures - 1 - magnitude));
  const rounded = Number(x.toFixed(decimals));
  return rounded
    .toLocaleString("en-US", { minimumFractionDigits: decimals, maximumFractionDigits: decimals })
    .replace("-", MINUS);
}

/** Like sig, with an explicit plus sign for increases. */
export function signed(x: number, figures = 2): string {
  const s = sig(x, figures);
  return x > 0 ? `+${s}` : s;
}

export function pValue(p: number): string {
  if (!Number.isFinite(p)) return "n/a";
  if (p < 0.001) return "< 0.001";
  if (p < 0.01) return p.toFixed(3);
  return p.toFixed(2);
}

/** "p < 0.001" or "p = 0.04", ready to drop into a sentence. */
export function pText(p: number | null): string {
  if (p === null) return "p unavailable";
  const v = pValue(p);
  return v.startsWith("<") ? `p ${v}` : `p = ${v}`;
}

export function withUnit(value: string, unit: string): string {
  if (unit === "") return value;
  return unit === "%" ? `${value}%` : `${value} ${unit}`;
}
