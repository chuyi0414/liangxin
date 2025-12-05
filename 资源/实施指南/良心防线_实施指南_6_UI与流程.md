# 《良心防线》实施指南 - 第六部分：UI 与流程

> 本文档教你实现游戏 UI 和流程控制。

---

## 一、阶段 6：UI 与流程

### 目标
- 主界面 HUD（资金、良心值、波次信息）
- 招募面板
- 暂停菜单
- 结算界面
- 流程控制（主菜单→战斗→结算）

---

## 二、使用框架的 UI 模块

CYFramework 内置了 UIModule，我们用它来管理面板。

### 2.1 创建 UI 基类

**创建文件**：`Assets/Scripts/LiangXin/UI/LiangXinPanel.cs`

```csharp
using UnityEngine;
using CYFramework.Runtime.Core.UI;

namespace LiangXin.UI
{
    /// <summary>
    /// 良心防线 UI 面板基类
    /// </summary>
    public abstract class LiangXinPanel : UIPanel
    {
        // 子类可以重写这些方法
    }
}
```

### 2.2 主界面 HUD

**创建文件**：`Assets/Scripts/LiangXin/UI/HUDPanel.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using CYFramework.Runtime.Core;

namespace LiangXin.UI
{
    /// <summary>
    /// 主界面 HUD
    /// 显示资金、良心值、波次信息等
    /// </summary>
    public class HUDPanel : LiangXinPanel
    {
        [Header("资源显示")]
        public Text goldText;
        public Text conscienceText;
        public Text blackHeartText;

        [Header("波次显示")]
        public Text waveText;
        public Text prepareTimerText;

        [Header("引用")]
        private BossController _boss;
        private WaveManager _waveManager;

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            _boss = FindObjectOfType<BossController>();
            _waveManager = FindObjectOfType<WaveManager>();
        }

        private void Update()
        {
            if (!IsOpen) return;

            UpdateResourceDisplay();
            UpdateWaveDisplay();
        }

        /// <summary>
        /// 更新资源显示
        /// </summary>
        private void UpdateResourceDisplay()
        {
            if (_boss == null) return;

            if (goldText != null)
                goldText.text = $"资金: {_boss.Data.Gold}";

            if (conscienceText != null)
                conscienceText.text = $"良心: {_boss.Data.Conscience}";

            if (blackHeartText != null)
                blackHeartText.text = $"黑心: {_boss.Data.BlackHeart}/100";
        }

        /// <summary>
        /// 更新波次显示
        /// </summary>
        private void UpdateWaveDisplay()
        {
            if (_waveManager == null) return;

            if (waveText != null)
            {
                waveText.text = $"第 {_waveManager.CurrentWave + 1} 波";
            }

            if (prepareTimerText != null)
            {
                if (_waveManager.State == WaveState.Preparing)
                {
                    prepareTimerText.gameObject.SetActive(true);
                    prepareTimerText.text = $"布阵时间: {_waveManager.PrepareTimeLeft:F1}s";
                }
                else
                {
                    prepareTimerText.gameObject.SetActive(false);
                }
            }
        }
    }
}
```

### 2.3 招募面板

**创建文件**：`Assets/Scripts/LiangXin/UI/RecruitPanel.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CYFramework.Runtime.Core;
using LiangXin.Data;

namespace LiangXin.UI
{
    /// <summary>
    /// 招募面板
    /// </summary>
    public class RecruitPanel : LiangXinPanel
    {
        [Header("UI 元素")]
        public Transform cardContainer;  // 卡片容器
        public Button refreshButton;     // 刷新按钮
        public Text refreshCostText;     // 刷新费用

        [Header("设置")]
        public int cardsToShow = 3;      // 显示几张卡
        public int refreshCost = 20;     // 刷新费用

        [Header("预制体")]
        public GameObject recruitCardPrefab;

        private List<UnitConfig> _currentCards = new List<UnitConfig>();
        private UnitManager _unitManager;
        private BossController _boss;
        private LiangXinGame _game;

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            _unitManager = FindObjectOfType<UnitManager>();
            _boss = FindObjectOfType<BossController>();
            _game = FindObjectOfType<LiangXinGame>();

            if (refreshButton != null)
                refreshButton.onClick.AddListener(OnRefreshClicked);

            RefreshCards();
        }

        protected override void OnClose()
        {
            if (refreshButton != null)
                refreshButton.onClick.RemoveListener(OnRefreshClicked);

            base.OnClose();
        }

        /// <summary>
        /// 刷新可招募的卡片
        /// </summary>
        public void RefreshCards()
        {
            _currentCards.Clear();

            // 获取所有友军配置
            var allConfigs = new List<UnitConfig>();
            foreach (var config in _unitManager.unitConfigs)
            {
                if (!config.isEnemy)
                    allConfigs.Add(config);
            }

            // 随机选几个
            for (int i = 0; i < cardsToShow && allConfigs.Count > 0; i++)
            {
                int idx = Random.Range(0, allConfigs.Count);
                _currentCards.Add(allConfigs[idx]);
                allConfigs.RemoveAt(idx);
            }

            // 更新 UI
            UpdateCardDisplay();
        }

        /// <summary>
        /// 更新卡片显示
        /// </summary>
        private void UpdateCardDisplay()
        {
            // 清除旧卡片
            if (cardContainer != null)
            {
                foreach (Transform child in cardContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            // 创建新卡片
            foreach (var config in _currentCards)
            {
                CreateRecruitCard(config);
            }

            // 更新刷新费用
            if (refreshCostText != null)
                refreshCostText.text = $"刷新 ({refreshCost}G)";
        }

        /// <summary>
        /// 创建招募卡片
        /// </summary>
        private void CreateRecruitCard(UnitConfig config)
        {
            if (cardContainer == null) return;

            GameObject card;
            if (recruitCardPrefab != null)
            {
                card = Instantiate(recruitCardPrefab, cardContainer);
            }
            else
            {
                // 简易版：用按钮代替
                card = new GameObject(config.unitName);
                card.transform.SetParent(cardContainer);

                var button = card.AddComponent<Button>();
                var text = new GameObject("Text").AddComponent<Text>();
                text.transform.SetParent(card.transform);
                text.text = $"{config.unitName}\n{GetRecruitCost(config)}G";
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.black;

                var image = card.AddComponent<Image>();
                image.color = config.unitColor;

                var rt = card.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(100, 120);
            }

            // 点击招募
            var btn = card.GetComponent<Button>();
            if (btn != null)
            {
                string configId = config.unitId; // 闭包
                btn.onClick.AddListener(() => OnRecruitClicked(configId));
            }
        }

        /// <summary>
        /// 获取招募费用
        /// </summary>
        private int GetRecruitCost(UnitConfig config)
        {
            // 基础 30，根据基础属性调整
            return 30 + config.baseAtk;
        }

        /// <summary>
        /// 点击招募
        /// </summary>
        private void OnRecruitClicked(string configId)
        {
            var config = _unitManager.GetConfig(configId);
            if (config == null) return;

            int cost = GetRecruitCost(config);

            if (_boss.Data.Gold < cost)
            {
                Debug.Log("资金不足！");
                return;
            }

            // 扣钱
            _boss.Data.Gold -= cost;

            // 在基地附近生成
            Vector3 spawnPos = new Vector3(
                Random.Range(-2f, 2f),
                0,
                Random.Range(-2f, 2f)
            );

            _game.SpawnUnit(configId, spawnPos);
            Debug.Log($"招募了 {config.unitName}！");

            // 刷新卡片
            RefreshCards();
        }

        /// <summary>
        /// 点击刷新
        /// </summary>
        private void OnRefreshClicked()
        {
            if (_boss.Data.Gold < refreshCost)
            {
                Debug.Log("资金不足，无法刷新！");
                return;
            }

            _boss.Data.Gold -= refreshCost;
            RefreshCards();
        }
    }
}
```

### 2.4 暂停菜单

**创建文件**：`Assets/Scripts/LiangXin/UI/PausePanel.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using CYFramework.Runtime.Core;

namespace LiangXin.UI
{
    /// <summary>
    /// 暂停菜单
    /// </summary>
    public class PausePanel : LiangXinPanel
    {
        [Header("按钮")]
        public Button resumeButton;
        public Button settingsButton;
        public Button quitButton;

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            // 暂停游戏
            CYFW.World?.Pause();
            Time.timeScale = 0f;

            if (resumeButton != null)
                resumeButton.onClick.AddListener(OnResumeClicked);
            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitClicked);
        }

        protected override void OnClose()
        {
            // 恢复游戏
            Time.timeScale = 1f;
            CYFW.World?.Resume();

            if (resumeButton != null)
                resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (quitButton != null)
                quitButton.onClick.RemoveListener(OnQuitClicked);

            base.OnClose();
        }

        private void OnResumeClicked()
        {
            CYFW.UI.ClosePanel<PausePanel>();
        }

        private void OnQuitClicked()
        {
            Time.timeScale = 1f;
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
    }
}
```

---

## 三、创建 UI 预制体

### 3.1 在 Unity 中创建 Canvas

1. `GameObject → UI → Canvas`
2. Canvas Scaler 设置：
   - UI Scale Mode: Scale With Screen Size
   - Reference Resolution: 1920 x 1080

### 3.2 创建 HUD 预制体

1. 在 Canvas 下创建空物体 `HUDPanel`
2. 添加 `HUDPanel` 脚本
3. 创建 Text 子物体：GoldText, ConscienceText, WaveText 等
4. 把 Text 拖到脚本对应字段
5. 保存为预制体：`Assets/Prefabs/UI/HUDPanel.prefab`

### 3.3 创建其他面板预制体

同样方式创建：
- `RecruitPanel.prefab`
- `PausePanel.prefab`

---

## 四、集成到游戏

### 4.1 打开 HUD

在 `LiangXinGame.Start()` 中：

```csharp
// 打开 HUD
CYFW.UI.OpenPanel<HUDPanel>();
```

### 4.2 按键控制

在 `LiangXinGame.Update()` 中：

```csharp
// ESC - 暂停
if (Input.GetKeyDown(KeyCode.Escape))
{
    if (CYFW.UI.IsOpen<PausePanel>())
        CYFW.UI.ClosePanel<PausePanel>();
    else
        CYFW.UI.OpenPanel<PausePanel>();
}

// Tab - 招募面板
if (Input.GetKeyDown(KeyCode.Tab))
{
    if (CYFW.UI.IsOpen<RecruitPanel>())
        CYFW.UI.ClosePanel<RecruitPanel>();
    else
        CYFW.UI.OpenPanel<RecruitPanel>();
}
```

---

## 五、流程控制（可选）

如果你想要完整的 主菜单→战斗→结算 流程：

### 5.1 创建流程

**创建文件**：`Assets/Scripts/LiangXin/Procedures/MainMenuProcedure.cs`

```csharp
using CYFramework.Runtime.Core;

namespace LiangXin.Procedures
{
    public class MainMenuProcedure : ProcedureBase
    {
        public override void OnEnter()
        {
            // 显示主菜单 UI
            // CYFW.UI.OpenPanel<MainMenuPanel>();
        }

        public override void OnUpdate(float deltaTime)
        {
            // 等待玩家点击开始
        }

        public override void OnExit()
        {
            // CYFW.UI.ClosePanel<MainMenuPanel>();
        }
    }
}
```

### 5.2 流程切换

```csharp
// 从主菜单进入战斗
CYFW.Procedure.StartProcedure<BattleProcedure>();

// 战斗结束进入结算
CYFW.Procedure.StartProcedure<ResultProcedure>();
```

---

## 六、下一步

阶段 6 完成了。你现在有：
- ✅ HUD 显示资源和波次
- ✅ 招募面板
- ✅ 暂停菜单
- ✅ 基础流程框架

**最后阶段（阶段 7）**：扩展内容
- 更多职业和敌人
- 圣物系统
- 随机事件
- 局外成长

请查看 **良心防线_实施指南_7_扩展内容.md**。
