"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import { X, Clock, Database, FileText, Cpu, ChevronDown, ChevronUp } from "lucide-react";
import { useState } from "react";

export function TraceDrawer() {
  const { userId, activeTraceId, setActiveTraceId } = useAppStore();
  const [showPrompt, setShowPrompt] = useState(false);

  const { data: trace, isLoading } = useQuery({
    queryKey: ["trace", userId, activeTraceId],
    queryFn: () => (activeTraceId ? api.getTrace(userId, activeTraceId) : null),
    enabled: Boolean(activeTraceId),
  });

  if (!activeTraceId) return null;

  return (
    <div className="fixed inset-y-0 right-0 w-full sm:w-[480px] bg-white shadow-2xl border-l border-slate-200 z-50 flex flex-col transition-all">
      {/* 드로어 헤더 */}
      <div className="p-4 border-b border-slate-200 flex items-center justify-between bg-slate-50">
        <div className="flex items-center space-x-2">
          <Database className="w-4 h-4 text-indigo-600" />
          <h3 className="text-sm font-bold text-slate-800">RAG 실행 Trace</h3>
        </div>
        <button
          onClick={() => setActiveTraceId(null)}
          className="p-1 text-slate-400 hover:text-slate-700 hover:bg-slate-200 rounded-lg transition"
        >
          <X className="w-4 h-4" />
        </button>
      </div>

      {/* 본문 */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4 text-xs">
        {isLoading ? (
          <div className="text-center py-10 text-slate-400">
            Trace 정보를 불러오는 중...
          </div>
        ) : !trace ? (
          <div className="text-center py-10 text-rose-500">
            Trace를 찾을 수 없습니다.
          </div>
        ) : (
          <>
            {/* 기본 메타데이터 카드 */}
            <div className="grid grid-cols-2 gap-2 bg-slate-50 p-3 rounded-lg border border-slate-200">
              <div className="flex items-center space-x-1.5 text-slate-600">
                <Cpu className="w-3.5 h-3.5 text-indigo-500" />
                <span>모델: <strong className="text-slate-800">{trace.model_name || "N/A"}</strong></span>
              </div>
              <div className="flex items-center space-x-1.5 text-slate-600">
                <Clock className="w-3.5 h-3.5 text-amber-500" />
                <span>지연시간: <strong className="text-slate-800">{trace.latency_ms ?? 0}ms</strong></span>
              </div>
            </div>

            {/* 검색된 청크 목록 */}
            <div className="space-y-2">
              <h4 className="font-bold text-slate-700 flex items-center justify-between">
                <span>검색된 문서 청크 (Retrieval)</span>
                <span className="text-[11px] font-normal text-slate-500">
                  {trace.retrieved_chunks.length}개 검색됨
                </span>
              </h4>

              {trace.retrieved_chunks.length === 0 ? (
                <div className="text-slate-400 italic p-3 bg-slate-50 rounded-lg border border-slate-200">
                  일치하는 문서 청크가 없습니다.
                </div>
              ) : (
                trace.retrieved_chunks.map((chunk, idx) => (
                  <div
                    key={chunk.chunk_id || idx}
                    className="border border-slate-200 rounded-lg p-3 bg-white space-y-1.5 shadow-sm"
                  >
                    <div className="flex items-center justify-between text-slate-500">
                      <div className="flex items-center space-x-1 text-indigo-600 font-semibold truncate">
                        <FileText className="w-3 h-3 flex-shrink-0" />
                        <span className="truncate">{chunk.source_file_name}</span>
                        <span className="text-slate-400 font-normal">
                          (p.{chunk.page_number})
                        </span>
                      </div>
                      <span className="bg-indigo-50 text-indigo-700 font-mono text-[10px] px-1.5 py-0.5 rounded border border-indigo-100 flex-shrink-0">
                        유사도 {chunk.score.toFixed(3)}
                      </span>
                    </div>
                    <p className="text-slate-700 leading-relaxed bg-slate-50 p-2 rounded border border-slate-100 whitespace-pre-wrap font-sans">
                      {chunk.text}
                    </p>
                  </div>
                ))
              )}
            </div>

            {/* 전달된 프롬프트 (Prompt) */}
            {trace.prompt && (
              <div className="border border-slate-200 rounded-lg overflow-hidden">
                <button
                  onClick={() => setShowPrompt(!showPrompt)}
                  className="w-full p-2.5 bg-slate-50 hover:bg-slate-100 flex items-center justify-between font-bold text-slate-700 transition"
                >
                  <span>LLM에 전달된 프롬프트 전문</span>
                  {showPrompt ? (
                    <ChevronUp className="w-4 h-4" />
                  ) : (
                    <ChevronDown className="w-4 h-4" />
                  )}
                </button>
                {showPrompt && (
                  <div className="p-3 bg-slate-900 text-slate-100 font-mono text-[11px] whitespace-pre-wrap max-h-60 overflow-y-auto leading-relaxed">
                    {trace.prompt}
                  </div>
                )}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
