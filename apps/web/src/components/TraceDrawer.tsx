"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import {
  X,
  Clock,
  FileText,
  Cpu,
  ChevronDown,
  ChevronUp,
  Activity,
  Loader2,
  AlertCircle,
} from "lucide-react";
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
    <div className="fixed inset-y-0 right-0 w-full sm:w-[500px] bg-white shadow-drawer border-l border-kds-gray-300 z-50 flex flex-col">
      {/* 드로어 헤더 */}
      <div className="h-12 px-5 border-b border-kds-gray-300 flex items-center justify-between bg-kds-gray-50 flex-shrink-0">
        <div className="flex items-center space-x-2">
          <Activity className="w-4 h-4 text-kds-blue-600 stroke-[1.5]" />
          <h3 className="text-xs font-bold text-kds-gray-900 tracking-tight">
            검색 및 실행 분석 (Trace)
          </h3>
        </div>
        <button
          onClick={() => setActiveTraceId(null)}
          className="p-1 text-kds-gray-500 hover:text-kds-gray-900 hover:bg-kds-gray-200 rounded transition-colors"
          title="닫기"
        >
          <X className="w-4 h-4 stroke-[1.5]" />
        </button>
      </div>

      {/* 드로어 본문 */}
      <div className="flex-1 overflow-y-auto p-5 space-y-4 text-xs bg-kds-gray-50">
        {isLoading ? (
          <div className="flex flex-col items-center justify-center py-20 text-kds-gray-500 space-y-2">
            <Loader2 className="w-5 h-5 animate-spin text-kds-blue-600" />
            <span className="text-xs">실행 추적 정보를 불러오는 중입니다...</span>
          </div>
        ) : !trace ? (
          <div className="flex items-center space-x-2 p-4 bg-white border border-kds-gray-300 rounded text-kds-red-600">
            <AlertCircle className="w-4 h-4 stroke-[1.5] flex-shrink-0" />
            <span>Trace 정보를 찾을 수 없습니다.</span>
          </div>
        ) : (
          <>
            {/* 메타데이터 요약 카드 */}
            <div className="bg-white p-4 rounded border border-kds-gray-300 grid grid-cols-2 gap-4">
              <div className="space-y-1">
                <span className="text-[11px] font-medium text-kds-gray-500 flex items-center space-x-1.5">
                  <Cpu className="w-3.5 h-3.5 text-kds-gray-500 stroke-[1.5]" />
                  <span>적용 모델</span>
                </span>
                <p className="font-mono text-xs text-kds-gray-900 font-medium truncate">
                  {trace.model_name || "N/A"}
                </p>
              </div>
              <div className="space-y-1">
                <span className="text-[11px] font-medium text-kds-gray-500 flex items-center space-x-1.5">
                  <Clock className="w-3.5 h-3.5 text-kds-gray-500 stroke-[1.5]" />
                  <span>응답 소요 시간</span>
                </span>
                <p className="font-mono text-xs text-kds-gray-900 font-medium">
                  {trace.latency_ms ?? 0} ms
                </p>
              </div>
            </div>

            {/* 검색된 문서 청크 목록 */}
            <div className="space-y-2.5">
              <div className="flex items-center justify-between">
                <h4 className="text-xs font-bold text-kds-gray-900 tracking-tight">
                  참조 문서 청크 (Retrieval)
                </h4>
                <span className="text-[11px] font-mono text-kds-gray-500 bg-kds-gray-200 px-1.5 py-0.5 rounded">
                  {trace.retrieved_chunks.length}개
                </span>
              </div>

              {trace.retrieved_chunks.length === 0 ? (
                <div className="text-kds-gray-500 text-xs p-4 bg-white rounded border border-kds-gray-300 text-center">
                  일치하는 문서 청크가 없습니다.
                </div>
              ) : (
                <div className="space-y-2.5">
                  {trace.retrieved_chunks.map((chunk, idx) => (
                    <div
                      key={chunk.chunk_id || idx}
                      className="border border-kds-gray-300 rounded bg-white p-3.5 space-y-2"
                    >
                      <div className="flex items-center justify-between text-xs">
                        <div className="flex items-center space-x-1.5 font-medium text-kds-gray-900 truncate">
                          <FileText className="w-3.5 h-3.5 text-kds-blue-600 stroke-[1.5] flex-shrink-0" />
                          <span className="truncate">{chunk.source_file_name}</span>
                          <span className="text-kds-gray-500 text-[11px] font-normal">
                            (p.{chunk.page_number})
                          </span>
                        </div>
                        <span className="bg-kds-blue-50 text-kds-blue-700 font-mono text-[11px] px-1.5 py-0.5 rounded border border-kds-blue-200 flex-shrink-0 ml-2">
                          유사도 {chunk.score.toFixed(3)}
                        </span>
                      </div>
                      <p className="text-xs text-kds-gray-700 leading-relaxed bg-kds-gray-50 p-2.5 rounded border border-kds-gray-200 whitespace-pre-wrap font-sans">
                        {chunk.text}
                      </p>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* 프롬프트 전문 아코디언 */}
            {trace.prompt && (
              <div className="border border-kds-gray-300 rounded overflow-hidden bg-white">
                <button
                  onClick={() => setShowPrompt(!showPrompt)}
                  className="w-full px-4 py-3 bg-white hover:bg-kds-gray-100 flex items-center justify-between text-xs font-semibold text-kds-gray-900 transition-colors"
                >
                  <span>프롬프트 전문 보기</span>
                  {showPrompt ? (
                    <ChevronUp className="w-4 h-4 text-kds-gray-500 stroke-[1.5]" />
                  ) : (
                    <ChevronDown className="w-4 h-4 text-kds-gray-500 stroke-[1.5]" />
                  )}
                </button>
                {showPrompt && (
                  <div className="p-4 bg-kds-gray-900 text-kds-gray-100 font-mono text-[11px] whitespace-pre-wrap max-h-64 overflow-y-auto leading-relaxed border-t border-kds-gray-300">
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
