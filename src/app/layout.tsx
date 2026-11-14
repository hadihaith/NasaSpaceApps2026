import type { Metadata } from "next";
import { Atkinson_Hyperlegible_Next, Bricolage_Grotesque } from "next/font/google";
import { SiteNav } from "@/components/SiteNav";
import "./globals.css";

const body = Atkinson_Hyperlegible_Next({
  variable: "--font-body",
  subsets: ["latin"],
  adjustFontFallback: false,
});

const display = Bricolage_Grotesque({
  variable: "--font-display",
  subsets: ["latin"],
  axes: ["wdth"],
});

export const metadata: Metadata = {
  title: "Operation: The Great Homecoming",
  description:
    "Join an alien crew, experiment with Earth's climate, and investigate the planet with real NASA data.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" className={`${body.variable} ${display.variable} antialiased`}>
      <body>
        <SiteNav />
        {children}
      </body>
    </html>
  );
}
