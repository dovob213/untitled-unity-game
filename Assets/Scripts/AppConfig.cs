using System;
using UnityEngine;

/// <summary>
/// 백엔드(Supabase) 및 AI API 설정 관리
/// 키는 .env 또는 런타임 보안 저장소에서 로드하도록 설계
/// </summary>
public static class AppConfig
{
    public static string SupabaseUrl { get; private set; } = "https://your-project.supabase.co";
    public static string SupabaseAnonKey { get; private set; } = string.Empty;
    public static string LlmApiKey { get; private set; } = string.Empty;

    public static bool IsInitialized { get; private set; }

    /// <summary>
    /// 백엔드 및 AI 서비스 초기화
    /// </summary>
    public static void Initialize(string supabaseUrl, string anonKey, string llmKey = "")
    {
        SupabaseUrl = supabaseUrl;
        SupabaseAnonKey = anonKey;
        LlmApiKey = llmKey;
        IsInitialized = true;

        Debug.Log("[AppConfig] 백엔드 설정 초기화 완료");
    }
}
