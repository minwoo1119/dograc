"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import {
  X,
  Clock,
  FileText,
  Cpu,
  Activity,
  Loader2,
  AlertCircle,
} from "lucide-react";

export function TraceDrawer() {
  const { userId, activeTraceId, setActiveTraceId } = useAppStore();

  const { data: trace, isLoading } = useQuery({
    queryKey: ["trace", userId, activeTraceId],
    queryFn: () => (activeTraceId ? api.getTrace(userId, activeTraceId) : null),
    enabled: Boolean(activeTraceId),
  });

  if (!activeTraceId) return null;

  return (
    <div className="fixed inset-y-0 right-0 w-full sm:w-[540px] bg-white shadow-drawer border-l border-kds-gray-300 z-50 flex flex-col animate-in slide-in-from-right duration-200">
      {/* 드로어 헤더 */}
      <div className="h-14 px-5 border-b border-kds-gray-300 flex items-center justify-between bg-white flex-shrink-0">
        <div className="flex items-center space-x-2.5">
          <Activity className="w-4 h-4 text-kds-blue-600 stroke-[1.75]" />
          <div>
            <h3 className="text-sm font-bold text-kds-gray-900 tracking-tight">
              답변 근거 및 출처 문서 분석
            </h3>
          </div>
        </div>
        <button
          onClick={() => setActiveTraceId(null)}
          className="p-1.5 text-kds-gray-500 hover:text-kds-gray-900 hover:bg-kds-gray-100 rounded-lg transition-colors"
          title="닫기"
        >
          <X className="w-4 h-4 stroke-[1.75]" />
        </button>
      </div>

      {/* 드로어 본문 */}
      <div className="flex-1 overflow-y-auto p-5 space-y-5 text-xs bg-kds-gray-50">
        {isLoading ? (
          <div className="flex flex-col items-center justify-center py-24 text-kds-gray-500 space-y-3">
            <Loader2 className="w-5 h-5 animate-spin text-kds-blue-600" />
            <span className="text-xs">출처 문서 분석 데이터를 불러오고 있습니다...</span>
          </div>
        ) : !trace ? (
          <div className="flex items-center space-x-2 p-4 bg-white border border-kds-gray-300 rounded text-kds-red-600">
            <AlertCircle className="w-4 h-4 stroke-[1.5] flex-shrink-0" />
            <span>분석 정보를 찾을 수 없습니다.</span>
          </div>
        ) : (
          <>
            {/* 메타데이터 요약 카드 */}
            <div className="bg-white p-4 rounded-lg border border-kds-gray-300 grid grid-cols-2 gap-4 shadow-xs">
              <div className="space-y-1">
                <span className="text-[11px] font-medium text-kds-gray-500 flex items-center space-x-1.5">
                  <Cpu className="w-3.5 h-3.5 text-kds-gray-500 stroke-[1.5]" />
                  <span>생성 언어 모델</span>
                </span>
                <p className="font-mono text-xs text-kds-gray-900 font-semibold truncate">
                  {trace.model_name || "N/A"}
                </p>
              </div>
              <div className="space-y-1">
                <span className="text-[11px] font-medium text-kds-gray-500 flex items-center space-x-1.5">
                  <Clock className="w-3.5 h-3.5 text-kds-gray-500 stroke-[1.5]" />
                  <span>답변 생성 시간</span>
                </span>
                <p className="font-mono text-xs text-kds-gray-900 font-semibold">
                  {trace.latency_ms ?? 0} ms
                </p>
              </div>
            </div>

            {/* 참고한 문서 출처 및 원문 발췌 목록 */}
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <div>
                  <h4 className="text-xs font-bold text-kds-gray-900 tracking-tight">
                    답변 생성에 직접 참고한 문서 발췌문
                  </h4>
                  <p className="text-[11px] text-kds-gray-500 mt-0.5">
                    코사인 유사도가 높은 상위 청크만 선별하여 답변 생성에 반영되었습니다.
                  </p>
                </div>
                <span className="text-[11px] font-mono font-semibold text-kds-blue-700 bg-kds-blue-50 border border-kds-blue-200 px-2 py-0.5 rounded flex-shrink-0">
                  {trace.retrieved_chunks.length}개 발췌
                </span>
              </div>

              {trace.retrieved_chunks.length === 0 ? (
                <div className="text-kds-gray-500 text-xs p-6 bg-white rounded-lg border border-kds-gray-300 text-center">
                  일치하는 문서 근거 청크가 없습니다.
                </div>
              ) : (
                <div className="space-y-3">
                  {trace.retrieved_chunks.map((chunk, idx) => (
                    <div
                      key={chunk.chunk_id || idx}
                      className="border border-kds-gray-300 rounded-lg bg-white p-4 space-y-3 shadow-xs hover:border-kds-blue-400 transition-colors"
                    >
                      {/* 출처 헤더: 파일명, 페이지, 유사도 점수 */}
                      <div className="flex items-center justify-between text-xs pb-2 border-b border-kds-gray-200">
                        <div className="flex items-center space-x-2 font-semibold text-kds-gray-900 truncate">
                          <FileText className="w-4 h-4 text-kds-blue-600 stroke-[1.75] flex-shrink-0" />
                          <span className="truncate max-w-[240px]" title={chunk.source_file_name}>
                            {chunk.source_file_name}
                          </span>
                          <span className="text-kds-blue-700 bg-kds-blue-50 border border-kds-blue-200 px-1.5 py-0.2 rounded text-[11px] font-normal flex-shrink-0">
                            p.{chunk.page_number}
                          </span>
                        </div>
                        <span className="bg-kds-gray-100 text-kds-gray-700 font-mono text-[11px] px-2 py-0.5 rounded border border-kds-gray-200 flex-shrink-0 ml-2 font-medium">
                          유사도 {(chunk.score * 100).toFixed(1)}%
                        </span>
                      </div>

                      {/* 발췌 원문 본문 */}
                      <div className="space-y-1">
                        <span className="text-[10px] font-semibold text-kds-gray-400 uppercase tracking-wider">
                          문서 원문 발췌 (Excerpt)
                        </span>
                        <p className="text-xs text-kds-gray-800 leading-relaxed bg-kds-gray-50 p-3 rounded border border-kds-gray-200 whitespace-pre-wrap font-sans max-h-52 overflow-y-auto">
                          {chunk.text}
                        </p>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
}
