import { Navbar } from "@/components/Navbar";
import { DocumentPanel } from "@/components/DocumentPanel";
import { ChatPanel } from "@/components/ChatPanel";
import { TraceDrawer } from "@/components/TraceDrawer";

export default function Home() {
  return (
    <div className="flex flex-col h-screen overflow-hidden bg-slate-100">
      <Navbar />

      <main className="flex-1 flex overflow-hidden max-w-7xl w-full mx-auto my-3 px-4 sm:px-6 lg:px-8 space-x-3">
        {/* 좌측: 문서 보관함 및 업로드 */}
        <aside className="w-80 sm:w-96 flex-shrink-0 h-full rounded-2xl overflow-hidden shadow-sm border border-slate-200">
          <DocumentPanel />
        </aside>

        {/* 우측: RAG 질의응답 채팅창 */}
        <section className="flex-1 h-full rounded-2xl overflow-hidden shadow-sm border border-slate-200">
          <ChatPanel />
        </section>
      </main>

      {/* RAG 실행 Trace 사이드 드로어 */}
      <TraceDrawer />
    </div>
  );
}
