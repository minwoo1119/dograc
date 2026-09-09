"use client";

import { useEffect, useState } from "react";
import { Navbar } from "@/components/Navbar";
import { DocumentPanel } from "@/components/DocumentPanel";
import { ChatPanel } from "@/components/ChatPanel";
import { TraceDrawer } from "@/components/TraceDrawer";
import { LandingPage } from "@/components/LandingPage";
import { useAppStore } from "@/lib/store";

export default function Home() {
  const { currentUser } = useAppStore();
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  // 마운트 전 서버 렌더링 시에는 랜딩 페이지를 기본 제공하여 Hydration mismatch 방지
  if (!mounted) {
    return <LandingPage />;
  }

  // 비로그인 사용자: 스크롤 가능한 소개 랜딩 페이지 노출
  if (!currentUser) {
    return <LandingPage />;
  }

  // 로그인 사용자: RAG 2열 작업대 노출
  return (
    <div className="flex flex-col h-full w-full overflow-hidden bg-kds-gray-50">
      <Navbar />

      <main className="flex-1 min-h-0 flex overflow-hidden max-w-[1440px] w-full mx-auto p-3 sm:p-4 md:p-5 gap-3 sm:gap-4">
        {/* 좌측: 문서 보관함 및 업로드 */}
        <aside className="w-80 sm:w-96 flex-shrink-0 h-full min-h-0 flex flex-col rounded border border-kds-gray-300 overflow-hidden bg-white">
          <DocumentPanel />
        </aside>

        {/* 우측: RAG 질의응답 채팅창 */}
        <section className="flex-1 min-w-0 h-full min-h-0 flex flex-col rounded border border-kds-gray-300 overflow-hidden bg-white">
          <ChatPanel />
        </section>
      </main>

      {/* RAG 실행 Trace 사이드 드로어 */}
      <TraceDrawer />
    </div>
  );
}
