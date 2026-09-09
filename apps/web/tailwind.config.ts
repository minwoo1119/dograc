import type { Config } from "tailwindcss";

const config: Config = {
  content: [
    "./src/pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/components/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/app/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        kds: {
          blue: {
            50: "#F5F6FC",
            100: "#EEF0FA",
            200: "#D6DAF3",
            600: "#5D62C4",
            700: "#5055B1", // Core Action / Accent
            800: "#3F4391", // Pressed
            900: "#2D3068",
          },
          green: {
            50: "#F4FAF2",
            100: "#EDF8E9",
            600: "#4DAC27",
            700: "#4DAC27", // Positive / Success
            800: "#3A8A1C",
          },
          red: {
            50: "#FEF2F2",
            100: "#FDE9EA",
            500: "#EC1F2D", // Negative / Error
            700: "#DA2128",
          },
          gray: {
            50: "#FAFBFC",
            100: "#F4F5F7", // bg-subtle
            200: "#EAEBEE", // bg-muted
            300: "#E0E2E6", // border 1px
            400: "#C9CCD2", // border-strong
            500: "#A6AAB2", // fg-tertiary
            600: "#80858E",
            700: "#4D5159", // fg-secondary
            800: "#2E3138",
            900: "#1B1D22", // fg (기본 본문)
          },
        },
      },
      fontFamily: {
        sans: [
          '"Noto Sans KR"',
          "Roboto",
          "-apple-system",
          "BlinkMacSystemFont",
          '"Apple SD Gothic Neo"',
          '"Malgun Gothic"',
          "sans-serif",
        ],
      },
      letterSpacing: {
        kds: "-0.01em",
      },
      boxShadow: {
        subtle: "0 1px 3px 0 rgba(0, 0, 0, 0.04)",
        card: "0 2px 6px 0 rgba(0, 0, 0, 0.03), 0 1px 2px 0 rgba(0, 0, 0, 0.02)",
        drawer: "-4px 0 24px 0 rgba(0, 0, 0, 0.06)",
        dropdown: "0 6px 16px 0 rgba(0, 0, 0, 0.08)",
      },
      borderRadius: {
        'kds-xs': '4px',
        'kds-s': '8px',
        'kds-m': '12px',
        'kds-l': '16px',
        'kds-xl': '20px',
      },
    },
  },
  plugins: [],
};
export default config;
