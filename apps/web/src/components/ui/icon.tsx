import type { SVGProps } from "react";

export type IconName =
  | "dashboard"
  | "content"
  | "idea"
  | "calendar"
  | "strategy"
  | "analytics"
  | "insight"
  | "connections"
  | "settings"
  | "menu"
  | "close"
  | "chevron"
  | "plus"
  | "logout";

const paths: Record<IconName, string> = {
  dashboard: "M3 3h7v7H3zM14 3h7v4h-7zM14 10h7v11h-7zM3 14h7v7H3z",
  content: "M4 4h16M4 9h16M4 14h10M4 19h10",
  idea: "M9 18h6m-5 3h4m-2-20a7 7 0 0 0-4 12.75c.5.35 1 1.25 1 2.25h6c0-1 .5-1.9 1-2.25A7 7 0 0 0 12 2z",
  calendar: "M7 2v4m10-4v4M3 9h18M5 4h14a2 2 0 0 1 2 2v14H3V6a2 2 0 0 1 2-2z",
  strategy: "M4 19V5m0 14h16M8 15l3-4 3 2 5-7",
  analytics: "M4 19V5m0 14h16M8 15v-4m4 4V7m4 8v-6",
  insight: "M12 3v2m0 14v2m9-9h-2M5 12H3m15.36 6.36-1.42-1.42M7.05 7.05 5.64 5.64m12.72 0-1.42 1.41M7.05 16.95l-1.41 1.41M15 10a3 3 0 1 0-6 0c0 1.1.5 1.8 1.2 2.5.5.5.8 1 .8 1.5h2c0-.5.3-1 .8-1.5.7-.7 1.2-1.4 1.2-2.5z",
  connections: "M10 13a5 5 0 0 0 7.07 0l3-3A5 5 0 0 0 13 2.93l-1.72 1.71m2.72 6.36a5 5 0 0 0-7.07 0l-3 3A5 5 0 0 0 11 21.07l1.71-1.71",
  settings: "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8zm0-6v2m0 16v2m10-10h-2M4 12H2m17.07-7.07-1.41 1.41M6.34 17.66l-1.41 1.41m14.14 0-1.41-1.41M6.34 6.34 4.93 4.93",
  menu: "M4 6h16M4 12h16M4 18h16",
  close: "m6 6 12 12M18 6 6 18",
  chevron: "m9 18 6-6-6-6",
  plus: "M12 5v14M5 12h14",
  logout: "M10 17l5-5-5-5m5 5H3m9-9h6a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-6",
};

export function Icon({
  name,
  ...props
}: SVGProps<SVGSVGElement> & { name: IconName }) {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <path d={paths[name]} />
    </svg>
  );
}
