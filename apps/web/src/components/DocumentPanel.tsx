"use client";

import { useRef, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import { DocumentStatus } from "@/types";
import {
  FileText,
  Upload,
  Play,
  Trash2,
  CheckCircle2,
  Clock,
  AlertCircle,
  Loader2,
} from "lucide-react";

export function DocumentPanel() {
  const queryClient = useQueryClient();
  const { userId, currentWorkspaceId } = useAppStore();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { data: documents = [], isLoading } = useQuery({
    queryKey: ["documents", userId, currentWorkspaceId],
    queryFn: () =>
      currentWorkspaceId ? api.getDocuments(userId, currentWorkspaceId) : [],
    enabled: Boolean(currentWorkspaceId),
  });

  const uploadMutation = useMutation({
    mutationFn: (file: File) => {
      if (!currentWorkspaceId) throw new Error("워크스페이스를 먼저 선택하세요.");
      return api.uploadDocument(userId, currentWorkspaceId, file);
    },
    onSuccess: (newDoc) => {
      queryClient.invalidateQueries({
        queryKey: ["documents", userId, currentWorkspaceId],
      });
      setErrorMessage(null);
      // 업로드 완료 후 자동으로 색인(처리) 시도
      processMutation.mutate(newDoc.id);
    },
    onError: (err) => setErrorMessage(err.message),
  });

  const processMutation = useMutation({
    mutationFn: (docId: string) => api.processDocument(userId, docId),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["documents", userId, currentWorkspaceId],
      });
      setErrorMessage(null);
    },
    onError: (err) => setErrorMessage(err.message),
  });

  const deleteMutation = useMutation({
    mutationFn: (docId: string) => api.deleteDocument(userId, docId),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["documents", userId, currentWorkspaceId],
      });
    },
  });

  const handleFiles = (files: FileList | null) => {
    if (!files || files.length === 0) return;
    const file = files[0];
    const ext = file.name.split(".").pop()?.toLowerCase();
    if (ext !== "pdf" && ext !== "txt") {
      setErrorMessage("현재는 PDF 및 TXT 문서만 지원합니다.");
      return;
    }
    uploadMutation.mutate(file);
  };

  const getStatusBadge = (status: DocumentStatus) => {
    switch (status) {
      case "ready":
        return (
          <span className="flex items-center space-x-1 text-emerald-700 bg-emerald-50 border border-emerald-200 px-2 py-0.5 rounded-full text-xs">
            <CheckCircle2 className="w-3 h-3" />
            <span>색인 완료</span>
          </span>
        );
      case "processing":
        return (
          <span className="flex items-center space-x-1 text-blue-700 bg-blue-50 border border-blue-200 px-2 py-0.5 rounded-full text-xs">
            <Loader2 className="w-3 h-3 animate-spin" />
            <span>처리 중</span>
          </span>
        );
      case "uploaded":
        return (
          <span className="flex items-center space-x-1 text-amber-700 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded-full text-xs">
            <Clock className="w-3 h-3" />
            <span>대기 중</span>
          </span>
        );
      case "failed":
        return (
          <span className="flex items-center space-x-1 text-rose-700 bg-rose-50 border border-rose-200 px-2 py-0.5 rounded-full text-xs">
            <AlertCircle className="w-3 h-3" />
            <span>실패</span>
          </span>
        );
    }
  };

  if (!currentWorkspaceId) {
    return (
      <div className="h-full flex items-center justify-center p-6 text-center text-slate-400">
        <div>
          <FileText className="w-10 h-10 mx-auto mb-2 opacity-40" />
          <p className="text-sm">상단에서 워크스페이스를 먼저 선택하세요.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="h-full flex flex-col bg-white border-r border-slate-200">
      <div className="p-4 border-b border-slate-200">
        <h2 className="text-sm font-bold text-slate-800 flex items-center justify-between">
          <span>문서 보관함</span>
          <span className="text-xs font-normal text-slate-500">
            총 {documents.length}개
          </span>
        </h2>
      </div>

      {/* 파일 업로드 드롭 영역 */}
      <div className="p-4">
        <div
          onDragOver={(e) => {
            e.preventDefault();
            setDragOver(true);
          }}
          onDragLeave={() => setDragOver(false)}
          onDrop={(e) => {
            e.preventDefault();
            setDragOver(false);
            handleFiles(e.dataTransfer.files);
          }}
          onClick={() => fileInputRef.current?.click()}
          className={`border-2 border-dashed rounded-xl p-4 text-center cursor-pointer transition ${
            dragOver
              ? "border-indigo-500 bg-indigo-50"
              : "border-slate-300 hover:border-slate-400 bg-slate-50"
          }`}
        >
          <input
            type="file"
            ref={fileInputRef}
            onChange={(e) => handleFiles(e.target.files)}
            className="hidden"
            accept=".pdf,.txt"
          />
          {uploadMutation.isPending || processMutation.isPending ? (
            <div className="flex flex-col items-center py-2">
              <Loader2 className="w-6 h-6 text-indigo-600 animate-spin mb-1" />
              <p className="text-xs text-slate-600 font-medium">문서 업로드 및 색인 중...</p>
            </div>
          ) : (
            <div className="flex flex-col items-center py-1">
              <Upload className="w-6 h-6 text-slate-400 mb-1" />
              <p className="text-xs font-semibold text-slate-700">
                PDF 또는 TXT 파일 업로드
              </p>
              <p className="text-[11px] text-slate-400 mt-0.5">
                클릭하거나 파일을 드래그하여 놓으세요
              </p>
            </div>
          )}
        </div>

        {errorMessage && (
          <div className="mt-2 text-xs text-rose-600 bg-rose-50 p-2 rounded-md flex items-center space-x-1">
            <AlertCircle className="w-3.5 h-3.5 flex-shrink-0" />
            <span>{errorMessage}</span>
          </div>
        )}
      </div>

      {/* 문서 목록 */}
      <div className="flex-1 overflow-y-auto px-4 pb-4 space-y-2.5">
        {documents.length === 0 ? (
          <div className="text-center py-8 text-xs text-slate-400">
            업로드된 문서가 없습니다.
          </div>
        ) : (
          documents.map((doc) => (
            <div
              key={doc.id}
              className="border border-slate-200 rounded-lg p-3 hover:shadow-sm transition bg-white space-y-2"
            >
              <div className="flex items-start justify-between">
                <div className="flex items-center space-x-2 overflow-hidden">
                  <FileText className="w-4 h-4 text-indigo-500 flex-shrink-0" />
                  <span className="text-xs font-semibold text-slate-800 truncate" title={doc.source_file_name}>
                    {doc.source_file_name}
                  </span>
                </div>
                <button
                  onClick={() => {
                    if (confirm(`'${doc.source_file_name}' 문서를 삭제하시겠습니까?`)) {
                      deleteMutation.mutate(doc.id);
                    }
                  }}
                  className="text-slate-400 hover:text-red-500 transition p-1"
                  title="삭제"
                >
                  <Trash2 className="w-3.5 h-3.5" />
                </button>
              </div>

              <div className="flex items-center justify-between pt-1">
                {getStatusBadge(doc.status)}

                {doc.status === "uploaded" && (
                  <button
                    onClick={() => processMutation.mutate(doc.id)}
                    className="flex items-center space-x-1 text-[11px] text-indigo-600 hover:text-indigo-700 font-medium"
                  >
                    <Play className="w-3 h-3" />
                    <span>색인 실행</span>
                  </button>
                )}
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
