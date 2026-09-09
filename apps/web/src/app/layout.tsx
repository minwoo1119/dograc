import type { Metadata } from "next";
import "./globals.css";
import { Providers } from "@/components/providers";
import { ToastContainer } from "@/components/ToastContainer";

export const metadata: Metadata = {
  title: "dograc - 문서 기반 모듈형 RAG 시스템",
  description: "신뢰할 수 있는 인라인 출처 인용과 투명한 실행 추적(Trace)을 제공하는 오픈소스 RAG 플랫폼",
  icons: {
    icon: "/icon.svg",
    shortcut: "/icon.svg",
    apple: "/icon.svg",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="ko">
      <body className="antialiased min-h-screen w-full flex flex-col bg-kds-gray-50 text-kds-gray-900 font-sans">
        <Providers>{children}</Providers>
        <ToastContainer />
      </body>
    </html>
  );
}
