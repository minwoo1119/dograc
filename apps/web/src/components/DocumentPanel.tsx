"use client";

import { useRef, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import { DocumentStatus } from "@/types";
import {
  FileText,
  FileUp,
  Play,
  Trash2,
  CheckCircle2,
  Clock,
  AlertCircle,
  Loader2,
  FolderOpen,
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
      if (!currentWorkspaceId) throw new Error("워크스페이스를 먼저 선택해 주세요.");
      return api.uploadDocument(userId, currentWorkspaceId, file);
    },
    onSuccess: (newDoc) => {
      queryClient.invalidateQueries({
        queryKey: ["documents", userId, currentWorkspaceId],
      });
      setErrorMessage(null);
      // 업로드 완료 후 자동 색인 처리 트리거
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
      setErrorMessage("현재는 PDF 및 TXT 문서만 업로드할 수 있습니다.");
      return;
    }
    uploadMutation.mutate(file);
  };

  const renderStatusBadge = (status: DocumentStatus) => {
    switch (status) {
      case "ready":
        return (
          <span className="inline-flex items-center space-x-1 h-[22px] px-2 rounded text-[11px] font-medium bg-kds-green-100 text-kds-green-700">
            <CheckCircle2 className="w-3 h-3" strokeWidth={2} />
            <span>색인 완료</span>
          </span>
        );
      case "processing":
        return (
          <span className="inline-flex items-center space-x-1 h-[22px] px-2 rounded text-[11px] font-medium bg-kds-blue-100 text-kds-blue-700">
            <Loader2 className="w-3 h-3 animate-spin" strokeWidth={2} />
            <span>처리 중</span>
          </span>
        );
      case "uploaded":
        return (
          <span className="inline-flex items-center space-x-1 h-[22px] px-2 rounded text-[11px] font-medium bg-kds-gray-100 text-kds-gray-700">
            <Clock className="w-3 h-3" strokeWidth={2} />
            <span>대기 중</span>
          </span>
        );
      case "failed":
        return (
          <span className="inline-flex items-center space-x-1 h-[22px] px-2 rounded text-[11px] font-medium bg-kds-red-100 text-kds-red-500">
            <AlertCircle className="w-3 h-3" strokeWidth={2} />
            <span>실패</span>
          </span>
        );
    }
  };

  if (!currentWorkspaceId) {
    return (
      <div className="h-full flex flex-col items-center justify-center p-8 text-center bg-white">
        <FolderOpen className="w-8 h-8 text-kds-gray-400 mb-2 stroke-[1.5]" />
        <p className="text-xs font-medium text-kds-gray-700">
          워크스페이스를 먼저 선택해 주세요
        </p>
        <p className="text-[11px] text-kds-gray-500 mt-0.5">
          상단 메뉴에서 작업할 워크스페이스를 고르거나 새로 만들 수 있습니다.
        </p>
      </div>
    );
  }

  return (
    <div className="h-full flex flex-col bg-white">
      {/* 패널 타이틀 */}
      <div className="h-12 px-5 border-b border-kds-gray-300 flex items-center justify-between">
        <div className="flex items-center space-x-2">
          <span className="text-xs font-bold text-kds-gray-900 tracking-tight">문서 보관함</span>
          <span className="text-[11px] font-medium text-kds-blue-700 bg-kds-blue-50 px-1.5 py-0.2 rounded-full">
            {documents.length}
          </span>
        </div>
      </div>

      {/* 파일 업로드 드롭 영역 */}
      <div className="p-4 border-b border-kds-gray-200">
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
          className={`border border-dashed rounded-xl p-4 text-center cursor-pointer transition-colors ${
            dragOver
              ? "border-kds-blue-700 bg-kds-blue-50"
              : "border-kds-gray-300 hover:border-kds-gray-400 bg-kds-gray-50/70"
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
            <div className="flex flex-col items-center py-2 space-y-1.5">
              <Loader2 className="w-5 h-5 text-kds-blue-700 animate-spin" strokeWidth={2} />
              <p className="text-xs font-medium text-kds-gray-800">문서를 분석하고 색인하는 중입니다</p>
              <p className="text-[11px] text-kds-gray-500">페이지 분할 및 벡터 임베딩 생성 중...</p>
            </div>
          ) : (
            <div className="flex flex-col items-center py-1.5 space-y-1">
              <FileUp className="w-5 h-5 text-kds-gray-500 mb-0.5 stroke-[1.5]" />
              <p className="text-xs font-medium text-kds-gray-900">
                문서 파일 올리기
              </p>
              <p className="text-[11px] text-kds-gray-500">
                PDF 또는 TXT 파일을 여기에 끌어다 놓으세요
              </p>
            </div>
          )}
        </div>

        {errorMessage && (
          <div className="mt-2.5 p-2 rounded-lg bg-kds-red-50 border border-kds-red-100 flex items-center space-x-1.5 text-xs text-kds-red-500">
            <AlertCircle className="w-3.5 h-3.5 flex-shrink-0" strokeWidth={2} />
            <span>{errorMessage}</span>
          </div>
        )}
      </div>

      {/* 문서 목록 리스트 */}
      <div className="flex-1 overflow-y-auto p-4 space-y-2">
        {documents.length === 0 ? (
          <div className="text-center py-12 text-kds-gray-400 text-xs">
            보관된 문서가 없습니다.
          </div>
        ) : (
          documents.map((doc) => (
            <div
              key={doc.id}
              className="border border-kds-gray-300 rounded-lg p-3 hover:border-kds-gray-400 transition-colors bg-white space-y-2 group"
            >
              <div className="flex items-start justify-between space-x-2">
                <div className="flex items-center space-x-2 min-w-0">
                  <FileText className="w-4 h-4 text-kds-blue-700 flex-shrink-0 stroke-[1.75]" />
                  <span
                    className="text-xs font-medium text-kds-gray-900 truncate block"
                    title={doc.source_file_name}
                  >
                    {doc.source_file_name}
                  </span>
                </div>
                <button
                  onClick={() => {
                    if (confirm(`'${doc.source_file_name}' 문서를 삭제하시겠습니까?`)) {
                      deleteMutation.mutate(doc.id);
                    }
                  }}
                  className="opacity-0 group-hover:opacity-100 text-kds-gray-400 hover:text-kds-red-500 transition-opacity p-0.5"
                  title="문서 삭제"
                >
                  <Trash2 className="w-3.5 h-3.5" strokeWidth={1.75} />
                </button>
              </div>

              <div className="flex items-center justify-between pt-1">
                {renderStatusBadge(doc.status)}

                {doc.status === "uploaded" && (
                  <button
                    onClick={() => processMutation.mutate(doc.id)}
                    className="inline-flex items-center space-x-1 text-[11px] font-medium text-kds-blue-700 hover:text-kds-blue-800"
                  >
                    <Play className="w-3 h-3" strokeWidth={2} />
                    <span>색인 시작</span>
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
