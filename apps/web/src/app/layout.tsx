import type { Metadata } from "next";
import "./globals.css";
import { Providers } from "@/components/providers";

export const metadata: Metadata = {
  title: "dograc - 오픈소스 모듈형 RAG 시스템",
  description: "문서 기반 질문답변 및 투명한 RAG 파이프라인 웹 시스템",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="ko">
      <body className="antialiased min-h-screen flex flex-col bg-slate-100">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
