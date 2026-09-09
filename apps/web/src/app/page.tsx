import { Navbar } from "@/components/Navbar";
import { DocumentPanel } from "@/components/DocumentPanel";
import { ChatPanel } from "@/components/ChatPanel";
import { TraceDrawer } from "@/components/TraceDrawer";

export default function Home() {
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
