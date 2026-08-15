using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Q, W, E, R 키에 할당 가능한 로그라이크 시너지 효과 유형
/// </summary>
public enum SkillEffectType
{
    None,
    OverloadAoE,     // 주변 적에게 전기 충격파 광역 데미지
    CriticalStrike,  // 강력한 치명타 데미지 (단일 타겟 특화)
    ExplosiveBeat,   // 타격 지점 폭발 (주변 적 밀쳐내기 및 데미지)
    ChainLightning   // 주변 다른 적에게 연쇄 번개 전이
}

/// <summary>
/// 개별 시너지 모듈 데이터
/// </summary>
[Serializable]
public class SkillModule
{
    public string moduleName = "Basic Strike";
    public SkillEffectType effectType = SkillEffectType.None;
    public float effectValue = 1f;
    public float effectRadius = 3.5f;

    public SkillModule() { }

    public SkillModule(string name, SkillEffectType type, float value, float radius = 3.5f)
    {
        moduleName = name;
        effectType = type;
        effectValue = value;
        effectRadius = radius;
    }
}

/// <summary>
/// Q, W, E, R 키별 시너지 모듈 관리 및 실행 시스템
/// </summary>
public class SkillModuleSystem : MonoBehaviour
{
    [Header("Q / W / E / R 모듈 세팅")]
    [SerializeField] private SkillModule moduleQ = new SkillModule("Overload Shock", SkillEffectType.OverloadAoE, 1f, 3.5f);
    [SerializeField] private SkillModule moduleW = new SkillModule("Precision Crit", SkillEffectType.CriticalStrike, 2f, 0f);
    [SerializeField] private SkillModule moduleE = new SkillModule("Explosive Beat", SkillEffectType.ExplosiveBeat, 1.5f, 4f);
    [SerializeField] private SkillModule moduleR = new SkillModule("Chain Lightning", SkillEffectType.ChainLightning, 1f, 6f);

    public event Action<KeyCode, SkillModule, Enemy> OnModuleExecuted;

    /// <summary>
    /// 키에 할당된 모듈 가져오기
    /// </summary>
    public SkillModule GetModule(KeyCode key)
    {
        return key switch
        {
            KeyCode.Q => moduleQ,
            KeyCode.W => moduleW,
            KeyCode.E => moduleE,
            KeyCode.R => moduleR,
            _ => null
        };
    }

    /// <summary>
    /// 타격 시 모듈 효과 발동
    /// </summary>
    public void TriggerModule(KeyCode key, PlayerController player, Enemy primaryTarget)
    {
        SkillModule module = GetModule(key);
        if (module == null || module.effectType == SkillEffectType.None) return;

        Vector3 hitPos = primaryTarget != null ? primaryTarget.transform.position : player.transform.position;

        switch (module.effectType)
        {
            case SkillEffectType.OverloadAoE:
                ApplyOverloadAoE(hitPos, module.effectRadius, module.effectValue, primaryTarget);
                break;

            case SkillEffectType.CriticalStrike:
                if (primaryTarget != null)
                {
                    primaryTarget.TakeBlinkHit(module.effectValue);
                    Debug.Log($"[SkillSystem] <color=#FF0055>💥 Precision Crit 💥</color> Extra +{module.effectValue} dmg to [{primaryTarget.name}]");
                }
                break;

            case SkillEffectType.ExplosiveBeat:
                ApplyExplosiveBeat(hitPos, module.effectRadius, module.effectValue, primaryTarget);
                break;

            case SkillEffectType.ChainLightning:
                ApplyChainLightning(hitPos, module.effectRadius, module.effectValue, primaryTarget);
                break;
        }

        OnModuleExecuted?.Invoke(key, module, primaryTarget);
    }

    private void ApplyOverloadAoE(Vector3 center, float radius, float damage, Enemy excluded)
    {
        int hitCount = 0;
        var enemies = Enemy.ActiveEnemies;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e == null || e.IsDead || e == excluded) continue;

            if (Vector2.Distance(center, e.transform.position) <= radius)
            {
                e.TakeBlinkHit(damage);
                hitCount++;
            }
        }

        Debug.Log($"[SkillSystem] <color=#00CCFF>⚡ Overload AoE ⚡</color> Hit {hitCount} surrounding enemies with {damage} dmg");
    }

    private void ApplyExplosiveBeat(Vector3 center, float radius, float damage, Enemy excluded)
    {
        int hitCount = 0;
        var enemies = Enemy.ActiveEnemies;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e == null || e.IsDead || e == excluded) continue;

            float dist = Vector2.Distance(center, e.transform.position);
            if (dist <= radius)
            {
                e.TakeBlinkHit(damage);
                Rigidbody2D rb = e.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Vector2 knockDir = ((Vector2)e.transform.position - (Vector2)center).normalized;
                    rb.AddForce(knockDir * 8f, ForceMode2D.Impulse);
                }
                hitCount++;
            }
        }

        Debug.Log($"[SkillSystem] <color=#FF8800>💥 Explosive Beat 💥</color> Blew away {hitCount} enemies!");
    }

    private void ApplyChainLightning(Vector3 center, float maxRange, float damage, Enemy excluded)
    {
        Enemy nearestSecondary = null;
        float closestDist = float.MaxValue;
        var enemies = Enemy.ActiveEnemies;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e == null || e.IsDead || e == excluded) continue;

            float dist = Vector2.Distance(center, e.transform.position);
            if (dist <= maxRange && dist < closestDist)
            {
                closestDist = dist;
                nearestSecondary = e;
            }
        }

        if (nearestSecondary != null)
        {
            nearestSecondary.TakeBlinkHit(damage);
            Debug.Log($"[SkillSystem] <color=#CC44FF>⚡ Chain Lightning ⚡</color> Chained to [{nearestSecondary.name}] for {damage} dmg");
        }
    }
}
