"use client";

import { useToastStore } from "@/lib/toast";
import { AlertCircle, CheckCircle2, Info, X } from "lucide-react";

export function ToastContainer() {
  const { toasts, removeToast } = useToastStore();

  if (toasts.length === 0) return null;

  return (
    <div className="fixed bottom-5 right-5 z-50 flex flex-col gap-2 max-w-sm w-full pointer-events-none select-none">
      {toasts.map((item) => {
        const isError = item.type === "error";
        const isSuccess = item.type === "success";

        return (
          <div
            key={item.id}
            className="pointer-events-auto bg-white border border-kds-gray-300 rounded shadow-drawer p-3.5 flex items-start space-x-3 animate-in slide-in-from-bottom-3 fade-in duration-200"
          >
            {/* 상태 아이콘 */}
            <div className="flex-shrink-0 mt-0.5">
              {isError && (
                <AlertCircle className="w-4 h-4 text-kds-red-500 stroke-[2]" />
              )}
              {isSuccess && (
                <CheckCircle2 className="w-4 h-4 text-kds-green-600 stroke-[2]" />
              )}
              {!isError && !isSuccess && (
                <Info className="w-4 h-4 text-kds-blue-700 stroke-[2]" />
              )}
            </div>

            {/* 내용 영역 */}
            <div className="flex-1 min-w-0 pr-1">
              <div className="flex items-center space-x-1.5 flex-wrap">
                <span className="text-xs font-bold text-kds-gray-900">
                  {item.title}
                </span>
                {item.code && (
                  <span className="text-[10px] font-mono text-kds-red-600 bg-kds-red-50 px-1.5 py-0.5 rounded border border-kds-red-200 font-medium">
                    {item.code}
                  </span>
                )}
              </div>
              <p className="text-xs text-kds-gray-700 leading-relaxed mt-1 break-words">
                {item.message}
              </p>
            </div>

            {/* 닫기 버튼 */}
            <button
              onClick={() => removeToast(item.id)}
              className="flex-shrink-0 p-1 text-kds-gray-400 hover:text-kds-gray-700 hover:bg-kds-gray-100 rounded transition-colors"
              title="닫기"
            >
              <X className="w-3.5 h-3.5 stroke-[1.5]" />
            </button>
          </div>
        );
      })}
    </div>
  );
}
