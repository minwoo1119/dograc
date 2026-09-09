"use client";

import { useState } from "react";
import { useAppStore } from "@/lib/store";
import { X, Check, Copy, ExternalLink, Loader2, AlertCircle, CheckCircle2 } from "lucide-react";

interface OllamaGuideModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export function OllamaGuideModal({ isOpen, onClose }: OllamaGuideModalProps) {
  const {
    localOllamaEndpoint,
    localOllamaModel,
    setLocalOllamaEndpoint,
    setLocalOllamaModel,
    setSelectedModelType,
  } = useAppStore();

  const [endpoint, setEndpoint] = useState(localOllamaEndpoint);
  const [modelName, setModelName] = useState(localOllamaModel);
  const [copiedCmd, setCopiedCmd] = useState<string | null>(null);

  // 연결 테스트 상태
  const [isTesting, setIsTesting] = useState(false);
  const [testResult, setTestResult] = useState<{
    success: boolean;
    message: string;
    models?: string[];
  } | null>(null);

  if (!isOpen) return null;

  const handleCopy = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopiedCmd(text);
    setTimeout(() => setCopiedCmd(null), 2000);
  };

  const handleTestConnection = async () => {
    setIsTesting(true);
    setTestResult(null);

    try {
      // 로컬 Ollama 태그 목록 API 확인 시도
      const cleanEndpoint = endpoint.replace(/\/v1\/?$/, "").replace(/\/$/, "");
      const res = await fetch(`${cleanEndpoint}/api/tags`, {
        method: "GET",
      });

      if (!res.ok) {
        throw new Error(`HTTP ${res.status}: 응답이 올바르지 않습니다.`);
      }

      const data = await res.json();
      const availableModels = (data.models || []).map((m: any) => m.name);

      setTestResult({
        success: true,
        message: `연결 성공! ${availableModels.length}개의 로컬 모델이 감지되었습니다.`,
        models: availableModels,
      });

      // 만약 감지된 모델 중 현재 입력된 모델이 없으면 첫 번째 모델 자동 추천
      if (availableModels.length > 0 && !availableModels.includes(modelName)) {
        setModelName(availableModels[0]);
      }
    } catch (err: any) {
      setTestResult({
        success: false,
        message:
          "로컬 Ollama 서버에 연결할 수 없습니다. Ollama가 백그라운드에서 실행 중인지 확인해 주세요.",
      });
    } finally {
      setIsTesting(false);
    }
  };

  const handleSave = () => {
    setLocalOllamaEndpoint(endpoint.trim());
    setLocalOllamaModel(modelName.trim());
    setSelectedModelType("local");
    onClose();
  };

  return (
    <div className="fixed inset-0 bg-black/40 backdrop-blur-[2px] flex items-center justify-center z-50 p-4 animate-in fade-in duration-150">
      <div className="bg-white rounded-lg shadow-dropdown max-w-xl w-full border border-kds-gray-300 flex flex-col max-h-[90vh] overflow-hidden">
        {/* 모달 헤더 */}
        <div className="h-14 px-6 border-b border-kds-gray-200 flex items-center justify-between bg-kds-gray-50 flex-shrink-0">
          <div>
            <h2 className="text-sm font-bold text-kds-gray-900">
              내 컴퓨터 Ollama 모델 연동 및 세팅 가이드
            </h2>
            <p className="text-[11px] text-kds-gray-500 mt-0.5">
              사용자 PC의 로컬 GPU를 활용하여 100% 무료 및 보안 격리로 질의응답을 수행합니다.
            </p>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 text-kds-gray-400 hover:text-kds-gray-700 hover:bg-kds-gray-200 rounded transition-colors"
          >
            <X className="w-4 h-4 stroke-[1.75]" />
          </button>
        </div>

        {/* 모달 본문 */}
        <div className="flex-1 overflow-y-auto p-6 space-y-6 text-xs text-kds-gray-700">
          {/* 단계 1: 설치 */}
          <div className="p-4 rounded-lg border border-kds-gray-300 bg-kds-gray-50 space-y-2">
            <div className="flex items-center justify-between">
              <span className="font-bold text-kds-gray-900 text-xs">
                1단계. Ollama 설치 프로그램 다운로드
              </span>
              <a
                href="https://ollama.com/download"
                target="_blank"
                rel="noreferrer"
                className="inline-flex items-center space-x-1 text-kds-blue-700 hover:text-kds-blue-900 hover:underline font-medium text-[11px]"
              >
                <span>ollama.com 다운로드</span>
                <ExternalLink className="w-3 h-3" />
              </a>
            </div>
            <p className="text-[11px] text-kds-gray-600 leading-relaxed">
              공식 웹사이트에서 본인의 운영체제(Windows / macOS / Linux)에 맞는 설치 파일을 받아 설치합니다.
            </p>
          </div>

          {/* 단계 2: 모델 다운로드 명령어 */}
          <div className="space-y-2.5">
            <span className="font-bold text-kds-gray-900 text-xs">
              2단계. 터미널(PowerShell 또는 명령 프롬프트)에서 모델 실행
            </span>
            <p className="text-[11px] text-kds-gray-600">
              원하는 모델의 명령어를 복사하여 터미널에 입력하면 자동으로 모델 다운로드 및 준비가 완료됩니다.
            </p>

            <div className="space-y-2">
              {[
                {
                  name: "qwen2.5:7b",
                  desc: "한국어 및 문서 질의응답에 가장 최적화된 추천 모델 (4.7GB)",
                  cmd: "ollama run qwen2.5:7b",
                  recommended: true,
                },
                {
                  name: "llama3.1:8b",
                  desc: "Meta의 최신 고성능 오픈소스 언어 모델 (4.9GB)",
                  cmd: "ollama run llama3.1:8b",
                  recommended: false,
                },
                {
                  name: "gemma2:9b",
                  desc: "Google의 정밀한 추론형 경량 모델 (5.5GB)",
                  cmd: "ollama run gemma2:9b",
                  recommended: false,
                },
              ].map((item) => (
                <div
                  key={item.name}
                  className="p-3 bg-white rounded-lg border border-kds-gray-300 flex items-center justify-between gap-3 shadow-xs"
                >
                  <div className="min-w-0">
                    <div className="flex items-center space-x-2">
                      <span className="font-bold text-kds-gray-900">{item.name}</span>
                      {item.recommended && (
                        <span className="bg-kds-blue-50 text-kds-blue-700 border border-kds-blue-200 px-1.5 py-0.2 rounded text-[10px] font-semibold">
                          강력 추천
                        </span>
                      )}
                    </div>
                    <div className="text-[11px] text-kds-gray-500 mt-0.5 truncate">
                      {item.desc}
                    </div>
                  </div>

                  <button
                    onClick={() => handleCopy(item.cmd)}
                    className="inline-flex items-center space-x-1 px-2.5 py-1.5 bg-kds-gray-100 hover:bg-kds-gray-200 text-kds-gray-700 rounded text-xs font-medium transition-colors flex-shrink-0"
                    title="명령어 복사"
                  >
                    {copiedCmd === item.cmd ? (
                      <>
                        <Check className="w-3.5 h-3.5 text-kds-green-700" />
                        <span className="text-kds-green-700">복사됨</span>
                      </>
                    ) : (
                      <>
                        <Copy className="w-3.5 h-3.5" />
                        <span>명령어 복사</span>
                      </>
                    )}
                  </button>
                </div>
              ))}
            </div>
          </div>

          {/* 단계 3: 로컬 설정 및 연결 확인 */}
          <div className="space-y-3 pt-2 border-t border-kds-gray-200">
            <span className="font-bold text-kds-gray-900 text-xs">
              3단계. 로컬 연결 설정 및 테스트
            </span>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label className="block text-[11px] font-medium text-kds-gray-700 mb-1">
                  Ollama 엔드포인트 URL
                </label>
                <input
                  type="text"
                  value={endpoint}
                  onChange={(e) => setEndpoint(e.target.value)}
                  placeholder="http://localhost:11434"
                  className="w-full border border-kds-gray-300 rounded px-3 py-2 text-xs font-mono text-kds-gray-900 focus:outline-none focus:border-kds-blue-600 focus:ring-1 focus:ring-kds-blue-600 transition-colors"
                />
              </div>

              <div>
                <label className="block text-[11px] font-medium text-kds-gray-700 mb-1">
                  사용할 모델 이름
                </label>
                <input
                  type="text"
                  value={modelName}
                  onChange={(e) => setModelName(e.target.value)}
                  placeholder="qwen2.5:7b"
                  className="w-full border border-kds-gray-300 rounded px-3 py-2 text-xs font-mono text-kds-gray-900 focus:outline-none focus:border-kds-blue-600 focus:ring-1 focus:ring-kds-blue-600 transition-colors"
                />
              </div>
            </div>

            {/* 연결 테스트 버튼 및 결과 */}
            <div className="pt-1">
              <button
                type="button"
                onClick={handleTestConnection}
                disabled={isTesting}
                className="h-8 px-3.5 bg-kds-gray-100 hover:bg-kds-gray-200 text-kds-gray-800 rounded text-xs font-medium transition-colors inline-flex items-center space-x-1.5"
              >
                {isTesting ? (
                  <>
                    <Loader2 className="w-3.5 h-3.5 animate-spin text-kds-blue-600" />
                    <span>연결 확인 중...</span>
                  </>
                ) : (
                  <span>연결 상태 테스트</span>
                )}
              </button>

              {testResult && (
                <div
                  className={`mt-2 p-3 rounded border text-xs flex items-start space-x-2 ${
                    testResult.success
                      ? "bg-kds-green-100 text-kds-green-800 border-kds-green-200"
                      : "bg-kds-red-50 text-kds-red-700 border-kds-red-200"
                  }`}
                >
                  {testResult.success ? (
                    <CheckCircle2 className="w-4 h-4 text-kds-green-700 flex-shrink-0 mt-0.5" />
                  ) : (
                    <AlertCircle className="w-4 h-4 text-kds-red-600 flex-shrink-0 mt-0.5" />
                  )}
                  <div>
                    <div className="font-medium">{testResult.message}</div>
                    {testResult.models && testResult.models.length > 0 && (
                      <div className="mt-1 flex flex-wrap gap-1">
                        {testResult.models.map((m) => (
                          <button
                            key={m}
                            type="button"
                            onClick={() => setModelName(m)}
                            className="bg-white/80 hover:bg-white text-kds-gray-800 px-1.5 py-0.5 rounded text-[10.5px] font-mono border border-kds-gray-300"
                            title="이 모델로 선택"
                          >
                            {m} {m === modelName && "✓"}
                          </button>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* 모달 푸터 */}
        <div className="h-14 px-6 border-t border-kds-gray-200 bg-white flex items-center justify-between flex-shrink-0">
          <span className="text-[11px] text-kds-gray-500">
            로컬 모델 설정은 브라우저에 안전하게 저장됩니다.
          </span>
          <div className="flex items-center space-x-2">
            <button
              onClick={onClose}
              className="h-8 px-3.5 text-xs text-kds-gray-700 hover:bg-kds-gray-100 rounded transition-colors"
            >
              닫기
            </button>
            <button
              onClick={handleSave}
              className="h-8 px-4 bg-kds-blue-600 hover:bg-kds-blue-800 text-white text-xs font-semibold rounded transition-colors shadow-xs"
            >
              설정 저장 및 적용
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
