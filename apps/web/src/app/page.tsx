import { Navbar } from "@/components/Navbar";
import { DocumentPanel } from "@/components/DocumentPanel";
import { ChatPanel } from "@/components/ChatPanel";
import { TraceDrawer } from "@/components/TraceDrawer";

export default function Home() {
  return (
    <div className="flex flex-col h-screen overflow-hidden bg-kds-gray-50 antialiased">
      <Navbar />

      <main className="flex-1 flex overflow-hidden max-w-[1440px] w-full mx-auto p-4 sm:p-5 lg:p-6 gap-4">
        {/* 좌측: 문서 보관함 및 업로드 */}
        <aside className="w-80 sm:w-96 flex-shrink-0 h-full rounded border border-kds-gray-300 overflow-hidden bg-white">
          <DocumentPanel />
        </aside>

        {/* 우측: RAG 질의응답 채팅창 */}
        <section className="flex-1 h-full rounded border border-kds-gray-300 overflow-hidden bg-white">
          <ChatPanel />
        </section>
      </main>

      {/* RAG 실행 Trace 사이드 드로어 */}
      <TraceDrawer />
    </div>
  );
}
