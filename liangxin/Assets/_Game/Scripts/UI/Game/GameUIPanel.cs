using System; // Array 等基础类型引用
using System.Collections.Generic; // IReadOnlyList 等集合接口引用
using System.Text; // StringBuilder 引用
using CYFramework;
using CYFramework.Core.Timer;
using CYFramework.Core.UI;
using PrimeTween; // PrimeTween 动画系统引用
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[UIPrefab("Prefabs/UI/Game/GameUIPanel")]
public class GameUIPanel : UIPanel
{
    /// <summary>
    /// 暂停按钮
    /// </summary>
    [SerializeField] private Button _btnPause;

    /// <summary>资金文本</summary>
    [SerializeField] private TMP_Text _txtMoney;
    /// <summary>良心文本</summary>
    [SerializeField] private TMP_Text _txtConscience;
    /// <summary>黑心文本</summary>
    [SerializeField] private TMP_Text _txtBlackHeart;
    /// <summary>
    /// 公司良心
    /// </summary>
    [SerializeField] private TMP_Text _txtCompanyConscience;
    /// <summary>
    /// 公司污染
    /// </summary>
    [SerializeField] private TMP_Text _txtCompanyPollution;
    /// <summary>
    /// 公司滑动条
    /// </summary>
    [SerializeField] private Slider _sliderCompanyPollution;
    /// <summary>
    /// 波次倒计时
    /// </summary>
    [SerializeField] private TMP_Text _txtWaveCountdown;
    /// <summary>
    /// 波次阶段
    /// </summary>
    [SerializeField] private TMP_Text _txtStage;
    /// <summary>波次 UI 刷新计时器。</summary>
    private Timer _waveUiTimer;
    /// <summary>是否已订阅战斗数据事件。</summary>
    private bool _battleDataEventsSubscribed; // 战斗数据事件订阅标记

    [Header("人才库")]
    /// <summary>
    /// 人才库物体
    /// </summary>
    [SerializeField] private GameObject _goTalentPool;
    /// <summary>
    /// 人才库Content
    /// </summary>
    [SerializeField] private GameObject _goTalentPoolContent;
    /// <summary>
    /// 人才库子物体脚本缓存（与 _goTalentPoolContent 子物体一一对应）。
    /// </summary>
    private GoTalents[] _talentPoolItems = Array.Empty<GoTalents>(); // 人才库条目缓存数组
    /// <summary>
    /// 人才库子物体是否已缓存。
    /// </summary>
    private bool _hasCachedTalentPoolItems; // 人才库缓存标记
    /// <summary>
    /// 员工随机打散索引缓存（用于无重复抽取）。
    /// </summary>
    private int[] _employeeShuffleIndices = Array.Empty<int>(); // 员工索引缓存数组
    /// <summary>
    /// 风格字符串拼接器（复用避免频繁分配）。
    /// </summary>
    private readonly StringBuilder _styleTextBuilder = new StringBuilder(64); // 风格字符串构建器
    /// <summary>
    /// 人才库按钮（显示/隐藏）
    /// </summary>
    [SerializeField] private Button _btnShowHide;
    /// <summary>人才库 RectTransform 缓存。</summary>
    private RectTransform _talentPoolRectTransform; // 人才库 RectTransform 缓存
    /// <summary>人才库是否展开。</summary>
    private bool _isTalentPoolExpanded; // 人才库展开状态标记
    /// <summary>人才库展开目标本地坐标。</summary>
    private static readonly Vector3 TalentPoolExpandedLocalPosition = new Vector3(0f, 0f, 0f); // 人才库展开位置
    /// <summary>人才库收起目标本地坐标。</summary>
    private static readonly Vector3 TalentPoolCollapsedLocalPosition = new Vector3(500f, 0f, 0f); // 人才库收起位置
    /// <summary>人才库移动动画时长（秒）。</summary>
    [SerializeField] private float _talentPoolMoveDuration = 0.3f; // 人才库移动时长
    /// <summary>人才库移动 Tween 句柄。</summary>
    private Tween _talentPoolTween; // 人才库移动 Tween 句柄
    /// <summary>人才库 Tween 起点缓存。</summary>
    private Vector3 _talentPoolTweenFrom; // 人才库 Tween 起点缓存
    /// <summary>人才库 Tween 终点缓存。</summary>
    private Vector3 _talentPoolTweenTo; // 人才库 Tween 终点缓存

    protected override void OnBindUI()
    {
        base.OnBindUI();
        CacheTalentPoolItemsIfNeeded(); // 尝试缓存人才库子物体脚本引用
        if (_btnPause != null)
        {
            _btnPause.onClick.AddListener(OnBtnPauseClick);
        }
        if (_btnShowHide != null)
        {
            _btnShowHide.onClick.AddListener(OnBtnShowHideClick); // 绑定人才库显示/隐藏按钮事件
        }
        if (_goTalentPool != null)
        {
            _talentPoolRectTransform = _goTalentPool.GetComponent<RectTransform>(); // 缓存人才库 RectTransform
        }
    }

    /// <summary>
    /// 刷新人才库 Content 显示（从 Employee.csv 抽取，不可重复，数量不足则显示已有数量）。
    /// </summary>
    public void RefreshTalentPoolContent() // 人才库刷新入口
    {
        if (!TryGetTalentPoolItems(out var items, out var slotCount)) // 获取人才库条目缓存
        {
            return; // 无有效条目时直接退出
        }

        var targetDisplayCount = GetTalentPoolTargetDisplayCount(slotCount); // 获取目标显示数量
        var maxSlotCount = Mathf.Max(0, slotCount); // 计算槽位数量保护值
        var clampedTarget = Mathf.Clamp(targetDisplayCount, 0, maxSlotCount); // 将目标数量限制在槽位范围内

        if (!TryGetEmployeeRows(out var employeeRows, out var employeeCount)) // 获取员工数据表行列表
        {
            HideTalentPoolItems(items, slotCount); // 数据表不可用时隐藏所有条目
            return; // 直接退出
        }

        var showCount = Mathf.Min(clampedTarget, employeeCount, slotCount); // 计算最终显示数量（不足则按已有数量显示）
        if (showCount <= 0) // 最终显示数量判定
        {
            HideTalentPoolItems(items, slotCount); // 无需显示时隐藏所有条目
            return; // 直接退出
        }

        EnsureEmployeeShuffleIndices(employeeCount); // 确保索引缓存容量满足员工数量
        ShuffleEmployeeIndices(employeeCount); // 打散索引数组实现无重复抽取

        for (int i = 0; i < slotCount; i++) // 遍历人才库槽位
        {
            var item = items[i]; // 获取当前槽位脚本
            if (i >= showCount) // 超出显示数量判定
            {
                SetTalentItemActive(item, false, i); // 隐藏多余条目
                continue; // 继续下一个槽位
            }

            var employeeIndex = _employeeShuffleIndices[i]; // 获取本槽位对应的员工索引
            var employeeRow = employeeRows[employeeIndex]; // 获取员工数据行
            if (employeeRow == null) // 员工行为空判定
            {
                SetTalentItemActive(item, false, i); // 员工行为空时隐藏条目
                continue; // 继续下一个槽位
            }

            var styleText = BuildEmployeeStyleText(employeeRow); // 将 StyleIds 解析为风格字符串
            if (item != null) // 脚本存在判定
            {
                item.SetData(employeeRow, styleText); // 刷新条目显示
            }

            SetTalentItemActive(item, true, i); // 显示当前条目
        }
    }

    /// <summary>
    /// 缓存人才库子物体的 GoTalents 组件引用（避免刷新时反复 GetComponent）。
    /// </summary>
    private void CacheTalentPoolItemsIfNeeded() // 人才库组件缓存入口
    {
        if (_goTalentPoolContent == null) // Content 为空判定
        {
            return; // Content 为空时直接退出
        }

        var contentTransform = _goTalentPoolContent.transform; // 获取 Content Transform
        var childCount = contentTransform != null ? contentTransform.childCount : 0; // 获取子物体数量
        if (_hasCachedTalentPoolItems && _talentPoolItems != null && _talentPoolItems.Length == childCount) // 缓存有效判定
        {
            return; // 缓存有效时直接退出
        }

        if (childCount <= 0) // 子物体数量判定
        {
            _talentPoolItems = Array.Empty<GoTalents>(); // 无子物体时缓存空数组
            _hasCachedTalentPoolItems = true; // 标记缓存完成
            return; // 直接退出
        }

        _talentPoolItems = new GoTalents[childCount]; // 创建条目缓存数组
        for (int i = 0; i < childCount; i++) // 遍历子物体
        {
            var child = contentTransform.GetChild(i); // 获取子物体 Transform
            if (child == null) // 子物体为空判定
            {
                continue; // 子物体为空时跳过
            }

            var goTalents = child.GetComponent<GoTalents>(); // 获取子物体上的 GoTalents 组件
            if (goTalents == null) // 组件缺失判定
            {
                CY.LogWarning($"[GameUIPanel] 人才库子物体缺少 GoTalents 组件，Index={i}"); // 输出警告日志
            }

            _talentPoolItems[i] = goTalents; // 缓存组件引用（允许为空）
        }

        _hasCachedTalentPoolItems = true; // 标记缓存完成
    }

    /// <summary>
    /// 获取人才库条目缓存与槽位数量。
    /// </summary>
    /// <param name="items">输出条目数组。</param>
    /// <param name="slotCount">输出槽位数量。</param>
    /// <returns>是否存在有效槽位。</returns>
    private bool TryGetTalentPoolItems(out GoTalents[] items, out int slotCount) // 人才库条目获取入口
    {
        CacheTalentPoolItemsIfNeeded(); // 确保已缓存条目组件
        items = _talentPoolItems; // 输出缓存数组
        slotCount = items != null ? items.Length : 0; // 输出槽位数量
        return slotCount > 0; // 返回是否存在有效槽位
    }

    /// <summary>
    /// 获取人才库目标显示数量（优先读取 BattleData.json 配置，缺失则回退为槽位数量）。
    /// </summary>
    /// <param name="fallbackSlotCount">回退槽位数量。</param>
    /// <returns>目标显示数量。</returns>
    private int GetTalentPoolTargetDisplayCount(int fallbackSlotCount) // 人才库显示数量获取入口
    {
        var configuredCount = 0; // 默认配置数量
        var battleDataManager = CY.BattleDataManager; // 获取战斗数据管理器
        if (battleDataManager != null) // 管理器存在判定
        {
            var battleData = battleDataManager.BattleData; // 获取战斗数据配置
            if (battleData != null) // 配置存在判定
            {
                configuredCount = battleData.TalentPoolDisplayCount; // 读取人才库显示数量配置
            }
        }

        if (configuredCount > 0) // 配置有效判定
        {
            return configuredCount; // 返回配置数量
        }

        return fallbackSlotCount; // 配置无效时回退为槽位数量
    }

    /// <summary>
    /// 获取员工数据表行列表（Employee.csv）。
    /// </summary>
    /// <param name="rows">输出员工行列表。</param>
    /// <param name="count">输出员工数量。</param>
    /// <returns>是否获取成功。</returns>
    private bool TryGetEmployeeRows(out IReadOnlyList<EmployeeUnitRow> rows, out int count) // 员工数据获取入口
    {
        rows = null; // 默认输出为空
        count = 0; // 默认输出数量为 0

        var dataManager = CY.Data; // 获取数据表管理器
        if (dataManager == null) // 管理器为空判定
        {
            CY.LogWarning("[GameUIPanel] DataTableManager 未就绪，无法刷新人才库。"); // 输出警告日志
            return false; // 返回失败
        }

        const string employeeTableName = "Employee"; // 员工数据表名常量
        if (!dataManager.HasDataTable(employeeTableName)) // 数据表未加载判定
        {
            CY.LogWarning("[GameUIPanel] 员工数据表未加载，无法刷新人才库。"); // 输出警告日志
            return false; // 返回失败
        }

        var table = dataManager.GetDataTable<EmployeeUnitRow>(employeeTableName); // 获取员工数据表实例
        if (table == null) // 表实例为空判定
        {
            CY.LogWarning("[GameUIPanel] 员工数据表为空，无法刷新人才库。"); // 输出警告日志
            return false; // 返回失败
        }

        rows = table.GetAllRows(); // 获取所有员工行
        count = rows != null ? rows.Count : 0; // 获取员工数量
        return count > 0; // 返回是否存在有效员工行
    }

    /// <summary>
    /// 隐藏所有人才库条目。
    /// </summary>
    /// <param name="items">条目数组。</param>
    /// <param name="slotCount">槽位数量。</param>
    private void HideTalentPoolItems(GoTalents[] items, int slotCount) // 人才库隐藏入口
    {
        for (int i = 0; i < slotCount; i++) // 遍历槽位
        {
            SetTalentItemActive(items[i], false, i); // 隐藏当前条目
        }
    }

    /// <summary>
    /// 设置人才库条目显示/隐藏（组件缺失时仍尝试通过 Content 子物体隐藏）。
    /// </summary>
    /// <param name="item">条目组件。</param>
    /// <param name="active">是否显示。</param>
    /// <param name="index">槽位索引。</param>
    private void SetTalentItemActive(GoTalents item, bool active, int index) // 条目显隐设置入口
    {
        if (item != null) // 组件存在判定
        {
            item.gameObject.SetActive(active); // 通过组件 GameObject 设置显隐
            return; // 直接退出
        }

        if (_goTalentPoolContent == null) // Content 为空判定
        {
            return; // Content 为空时直接退出
        }

        var contentTransform = _goTalentPoolContent.transform; // 获取 Content Transform
        if (contentTransform == null) // Transform 为空判定
        {
            return; // Transform 为空时直接退出
        }

        if (index < 0 || index >= contentTransform.childCount) // 索引越界判定
        {
            return; // 越界时直接退出
        }

        var child = contentTransform.GetChild(index); // 获取子物体 Transform
        if (child == null) // 子物体为空判定
        {
            return; // 子物体为空时直接退出
        }

        child.gameObject.SetActive(active); // 通过子物体 GameObject 设置显隐
    }

    /// <summary>
    /// 确保员工索引缓存数组容量满足员工数量。
    /// </summary>
    /// <param name="employeeCount">员工数量。</param>
    private void EnsureEmployeeShuffleIndices(int employeeCount) // 索引缓存确保入口
    {
        if (employeeCount <= 0) // 数量无效判定
        {
            _employeeShuffleIndices = Array.Empty<int>(); // 数量无效时重置为空数组
            return; // 直接退出
        }

        if (_employeeShuffleIndices != null && _employeeShuffleIndices.Length == employeeCount) // 容量匹配判定
        {
            return; // 容量匹配时直接退出
        }

        _employeeShuffleIndices = new int[employeeCount]; // 创建/重建索引缓存数组
    }

    /// <summary>
    /// 打散员工索引数组（Fisher–Yates 洗牌，保证无重复）。
    /// </summary>
    /// <param name="employeeCount">员工数量。</param>
    private void ShuffleEmployeeIndices(int employeeCount) // 员工索引打散入口
    {
        for (int i = 0; i < employeeCount; i++) // 初始化索引数组
        {
            _employeeShuffleIndices[i] = i; // 写入顺序索引
        }

        for (int i = 0; i < employeeCount; i++) // Fisher–Yates 洗牌
        {
            var swapIndex = UnityEngine.Random.Range(i, employeeCount); // 获取交换索引
            if (swapIndex == i) // 无需交换判定
            {
                continue; // 相同索引时跳过
            }

            var temp = _employeeShuffleIndices[i]; // 缓存当前索引
            _employeeShuffleIndices[i] = _employeeShuffleIndices[swapIndex]; // 写入交换值
            _employeeShuffleIndices[swapIndex] = temp; // 写回缓存值
        }
    }

    /// <summary>
    /// 将 Employee.csv 的 StyleIds 解析为风格字符串（映射 UnitStyle.csv）。
    /// </summary>
    /// <param name="employeeRow">员工数据行。</param>
    /// <returns>风格字符串（如“近战、肉盾”）。</returns>
    private string BuildEmployeeStyleText(EmployeeUnitRow employeeRow) // 风格字符串构建入口
    {
        if (employeeRow == null) // 数据为空判定
        {
            return string.Empty; // 返回空字符串
        }

        if (!employeeRow.TryGetStyleIds(out var styleIds) || styleIds == null || styleIds.Length <= 0) // 风格 Id 无效判定
        {
            return string.Empty; // 返回空字符串
        }

        var unitManager = CY.Unit; // 获取单位管理器（用于查询风格表）
        if (unitManager == null) // 管理器为空判定
        {
            return string.Empty; // 返回空字符串
        }

        _styleTextBuilder.Clear(); // 清空构建器内容
        for (int i = 0; i < styleIds.Length; i++) // 遍历风格 Id
        {
            var styleId = styleIds[i]; // 获取当前风格 Id
            if (styleId <= 0) // Id 无效判定
            {
                continue; // Id 无效时跳过
            }

            if (!unitManager.TryGetUnitStyleRow(styleId, out var styleRow) || styleRow == null) // 风格行查询失败判定
            {
                continue; // 查询失败时跳过
            }

            var styleName = styleRow.Name; // 获取风格名称
            if (string.IsNullOrEmpty(styleName)) // 名称为空判定
            {
                continue; // 名称为空时跳过
            }

            if (_styleTextBuilder.Length > 0) // 分隔符追加判定
            {
                _styleTextBuilder.Append('、'); // 追加中文分隔符
            }

            _styleTextBuilder.Append(styleName); // 追加风格名称
        }

        return _styleTextBuilder.Length > 0 ? _styleTextBuilder.ToString() : string.Empty; // 返回构建结果
    }

    /// <summary>
    /// 暂停按钮
    /// </summary>
    private void OnBtnPauseClick()
    {
        CY.Procedure.ChangeProcedure<MainProcedure>();
    }

    /// <summary>
    /// 人才库显示/隐藏按钮点击回调。
    /// </summary>
    private void OnBtnShowHideClick() // 人才库按钮点击入口
    {
        if (!TryGetTalentPoolRectTransform(out var rectTransform))
        {
            return; // 未获取到 RectTransform 时直接退出
        }

        _isTalentPoolExpanded = !_isTalentPoolExpanded; // 切换人才库展开状态
        var targetPosition = _isTalentPoolExpanded ? TalentPoolExpandedLocalPosition : TalentPoolCollapsedLocalPosition; // 计算目标位置
        PlayTalentPoolMoveTween(rectTransform, targetPosition); // 播放人才库移动动画

        var eventSystem = UnityEngine.EventSystems.EventSystem.current; // 获取当前 EventSystem（用于清理按钮选中态）
        if (eventSystem == null) // EventSystem 缺失判定
        {
            return; // 缺失时直接退出（不影响原有显示/隐藏逻辑）
        }

        if (eventSystem.currentSelectedGameObject == _btnShowHide.gameObject) // 当前选中对象是该按钮判定
        {
            eventSystem.SetSelectedGameObject(null); // 清理选中态，避免按空格/回车触发 Submit 重复点击
        }
    }

    /// <summary>
    /// 获取并缓存人才库 RectTransform。
    /// </summary>
    /// <param name="rectTransform">输出 RectTransform。</param>
    /// <returns>是否获取成功。</returns>
    private bool TryGetTalentPoolRectTransform(out RectTransform rectTransform) // 人才库 RectTransform 获取入口
    {
        rectTransform = _talentPoolRectTransform; // 优先使用缓存引用
        if (rectTransform != null)
        {
            return true; // 缓存可用时直接返回成功
        }

        if (_goTalentPool == null)
        {
            return false; // 物体为空时返回失败
        }

        rectTransform = _goTalentPool.GetComponent<RectTransform>(); // 获取人才库 RectTransform
        _talentPoolRectTransform = rectTransform; // 缓存 RectTransform 引用
        return rectTransform != null; // 返回是否获取成功
    }

    /// <summary>
    /// 播放人才库移动动画。
    /// </summary>
    /// <param name="rectTransform">人才库 RectTransform。</param>
    /// <param name="targetLocalPosition">目标本地坐标。</param>
    private void PlayTalentPoolMoveTween(RectTransform rectTransform, Vector3 targetLocalPosition) // 人才库移动动画入口
    {
        if (rectTransform == null)
        {
            return; // RectTransform 为空时直接退出
        }

        _talentPoolRectTransform = rectTransform; // 缓存 RectTransform 引用
        StopTalentPoolTween(); // 停止旧的移动动画
        _talentPoolTweenFrom = rectTransform.anchoredPosition3D; // 记录动画起点位置
        _talentPoolTweenTo = targetLocalPosition; // 记录动画终点位置
        var duration = _talentPoolMoveDuration; // 读取动画时长
        if (duration <= 0f)
        {
            rectTransform.anchoredPosition3D = targetLocalPosition; // 时长无效时直接设置位置
            return; // 直接结束
        }

        _talentPoolTween = Tween.Custom<GameUIPanel>(this, 0f, 1f, duration, (self, t) => // 使用 PrimeTween 播放自定义位移动画
        {
            var targetRect = self._talentPoolRectTransform; // 获取当前缓存 RectTransform
            if (targetRect == null)
            {
                return; // RectTransform 为空时直接退出
            }

            var clamped = Mathf.Clamp01(t); // 限制进度范围
            var eased = 1f - Mathf.Pow(1f - clamped, 3f); // 计算缓出曲线进度
            var nextPosition = Vector3.Lerp(self._talentPoolTweenFrom, self._talentPoolTweenTo, eased); // 计算插值位置
            targetRect.anchoredPosition3D = nextPosition; // 写入人才库位置
        });
    }

    /// <summary>
    /// 停止人才库移动动画。
    /// </summary>
    private void StopTalentPoolTween() // 人才库移动动画停止入口
    {
        if (_talentPoolTween.isAlive)
        {
            _talentPoolTween.Stop(); // 停止正在播放的动画
        }
    }

    /// <summary>
    /// 重置人才库为收起状态。
    /// </summary>
    private void ResetTalentPoolToHidden() // 人才库重置入口
    {
        if (!TryGetTalentPoolRectTransform(out var rectTransform))
        {
            return; // 无法获取 RectTransform 时直接退出
        }

        StopTalentPoolTween(); // 停止移动动画
        rectTransform.anchoredPosition3D = TalentPoolCollapsedLocalPosition; // 重置到收起位置
        _isTalentPoolExpanded = false; // 重置展开状态
    }

    /// <summary>
    /// 面板打开时刷新显示
    /// </summary>
    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        EnsureBattleDataSubscribed(); // 确保订阅战斗数据事件
        RefreshBattleData();
        StartWaveUiTimer();
    }

    /// <summary>
    /// 面板刷新时同步显示
    /// </summary>
    protected override void OnRefresh(object userData)
    {
        base.OnRefresh(userData);
        RefreshBattleData();
    }

    /// <summary>
    /// 面板隐藏时重置人才库位置。
    /// </summary>
    protected override void OnHide() // 面板隐藏回调入口
    {
        base.OnHide(); // 调用父类隐藏回调
        ResetTalentPoolToHidden(); // 隐藏时重置人才库位置
    }

    protected override void OnUnbindUI()
    {
        base.OnUnbindUI();
        if (_btnPause != null)
        {
            _btnPause.onClick.RemoveListener(OnBtnPauseClick);
        }
        if (_btnShowHide != null)
        {
            _btnShowHide.onClick.RemoveListener(OnBtnShowHideClick); // 解绑人才库显示/隐藏按钮事件
        }

        UnsubscribeBattleDataEvents(); // 取消战斗数据事件订阅
        StopWaveUiTimer();
    }

    /// <summary>
    /// 确保订阅战斗数据事件。
    /// </summary>
    private void EnsureBattleDataSubscribed() // 战斗数据事件订阅入口
    {
        if (_battleDataEventsSubscribed)
        {
            return; // 已订阅时直接返回
        }

        CY.Event.Subscribe<CompanyConscienceChangedEvent>(OnCompanyConscienceChanged, this); // 订阅公司良心变化事件
        CY.Event.Subscribe<CompanyPollutionChangedEvent>(OnCompanyPollutionChanged, this); // 订阅公司污染变化事件
        CY.Event.Subscribe<MoneyChangedEvent>(OnMoneyChanged, this); // 订阅资金变化事件
        CY.Event.Subscribe<ConscienceChangedEvent>(OnConscienceChanged, this); // 订阅良心变化事件
        CY.Event.Subscribe<BlackHeartChangedEvent>(OnBlackHeartChanged, this); // 订阅黑心变化事件
        _battleDataEventsSubscribed = true; // 标记已订阅
    }

    /// <summary>
    /// 取消订阅战斗数据事件。
    /// </summary>
    private void UnsubscribeBattleDataEvents() // 战斗数据事件取消订阅入口
    {
        if (!_battleDataEventsSubscribed)
        {
            return; // 未订阅时直接返回
        }

        CY.Event.UnsubscribeAll(this); // 取消当前面板的事件订阅
        _battleDataEventsSubscribed = false; // 标记已取消订阅
    }

    /// <summary>
    /// 公司良心变化事件回调。
    /// </summary>
    /// <param name="evt">良心变化事件。</param>
    private void OnCompanyConscienceChanged(ref CompanyConscienceChangedEvent evt) // 良心事件回调入口
    {
        SetValueText(_txtCompanyConscience, evt.CurrentValue); // 刷新公司良心显示
    }

    /// <summary>
    /// 公司污染变化事件回调。
    /// </summary>
    /// <param name="evt">污染变化事件。</param>
    private void OnCompanyPollutionChanged(ref CompanyPollutionChangedEvent evt) // 污染事件回调入口
    {
        var percent = ToPercent(evt.CurrentValue, evt.ThresholdValue); // 计算污染百分比
        SetValueText(_txtCompanyPollution, percent, true); // 刷新公司污染显示
        SetCompanyPollutionScrollbar(percent); // 刷新污染滑动条
    }

    /// <summary>
    /// 资金变化事件回调。
    /// </summary>
    /// <param name="evt">资金变化事件。</param>
    private void OnMoneyChanged(ref MoneyChangedEvent evt) // 资金事件回调入口
    {
        SetValueText(_txtMoney, evt.CurrentValue); // 刷新资金显示
    }

    /// <summary>
    /// 良心变化事件回调。
    /// </summary>
    /// <param name="evt">良心变化事件。</param>
    private void OnConscienceChanged(ref ConscienceChangedEvent evt) // 良心事件回调入口
    {
        SetValueText(_txtConscience, evt.CurrentValue); // 刷新良心显示
    }

    /// <summary>
    /// 黑心变化事件回调。
    /// </summary>
    /// <param name="evt">黑心变化事件。</param>
    private void OnBlackHeartChanged(ref BlackHeartChangedEvent evt) // 黑心事件回调入口
    {
        SetValueText(_txtBlackHeart, evt.CurrentValue); // 刷新黑心显示
    }

    /// <summary>
    /// 读取 BattleDataManager 中的缓存数据并刷新文本
    /// </summary>
    private void RefreshBattleData()
    {
        var manager = CY.BattleDataManager;
        var data = manager != null ? manager.BattleData : null;

        if (data == null)
        {
            SetValueText(_txtMoney, "--");
            SetValueText(_txtConscience, "--");
            SetValueText(_txtBlackHeart, "--");
            SetValueText(_txtCompanyConscience, "--");
            SetValueText(_txtCompanyPollution, "--");
            SetCompanyPollutionScrollbar(0);
            return;
        }

        SetValueText(_txtMoney, manager.MoneyCurrent); // 刷新资金显示
        SetValueText(_txtConscience, manager.ConscienceCurrent); // 刷新良心显示
        SetValueText(_txtBlackHeart, manager.BlackHeartCurrent); // 刷新黑心显示
        var companyConscience = manager.CompanyConscienceCurrent; // 读取公司良心当前值
        var companyPollution = manager.CompanyPollutionCurrent; // 读取公司污染当前值
        var companyPollutionMax = data.CompanyPollution; // 读取公司污染阈值

        SetValueText(_txtCompanyConscience, companyConscience); // 刷新公司良心显示
        var pollutionPercent = ToPercent(companyPollution, companyPollutionMax); // 计算污染百分比
        SetValueText(_txtCompanyPollution, pollutionPercent, true);
        SetCompanyPollutionScrollbar(pollutionPercent);
        
    }

    private static void SetValueText(TMP_Text target, int value)
    {
        if (target == null) return;
        target.SetText("{0}", value);
    }

    private static void SetValueText(TMP_Text target, int value, bool suffixPercent)
    {
        if (target == null) return;
        if (suffixPercent)
        {
            target.SetText("{0}%", value);
            return;
        }

        target.SetText("{0}", value);
    }

    private static void SetValueText(TMP_Text target, string value)
    {
        if (target == null) return;
        target.SetText(value);
    }

    /// <summary>
    /// 将数值转换为 0-100 的百分比并做上下限保护。
    /// </summary>
    private static int ToPercent(int value, int max)
    {
        if (max <= 0) return 0;
        if (value <= 0) return 0;
        if (value >= max) return 100;
        return value * 100 / max;
    }

    /// <summary>
    /// 同步污染百分比到滚动条（0-1）。
    /// </summary>
    private void SetCompanyPollutionScrollbar(int percent)
    {
        if (_sliderCompanyPollution == null) return;
        if (percent <= 0)
        {
            _sliderCompanyPollution.value = 0f;
            return;
        }

        _sliderCompanyPollution.value = percent >= 100 ? 1f : percent / 100f;
    }

    /// <summary>
    /// 启动波次 UI 刷新计时器。
    /// </summary>
    private void StartWaveUiTimer()
    {
        StopWaveUiTimer();
        _waveUiTimer = CY.Timer.Loop(0.2f, UpdateWaveUi);
        UpdateWaveUi();
    }

    /// <summary>
    /// 停止波次 UI 刷新计时器。
    /// </summary>
    private void StopWaveUiTimer()
    {
        if (_waveUiTimer == null)
        {
            return;
        }

        _waveUiTimer.Stop();
        _waveUiTimer = null;
    }

    /// <summary>
    /// 刷新波次倒计时与阶段显示。
    /// </summary>
    private void UpdateWaveUi()
    {
        var waveManager = CY.Wave;
        if (waveManager == null)
        {
            SetWaveStageText("--");
            SetWaveCountdownText("--:--");
            return;
        }

        if (!waveManager.TryGetMainWaveDisplayStatus(out var displayIndex, out var stage, out var remaining)) // 获取显示波次编号与阶段数据
        {
            SetWaveStageText("--");
            SetWaveCountdownText("--:--");
            return;
        }

        SetWaveStageText(displayIndex, stage); // 刷新波次阶段显示
        var seconds = Mathf.CeilToInt(remaining);
        SetWaveCountdownText(seconds);
    }

    /// <summary>
    /// 设置波次阶段文本。
    /// </summary>
    private void SetWaveStageText(int waveId, WaveStage stage)
    {
        if (_txtStage == null)
        {
            return;
        }

        if (stage == WaveStage.Prepare)
        {
            _txtStage.SetText("第{0}波 准备中", waveId);
            return;
        }

        if (stage == WaveStage.Spawn)
        {
            _txtStage.SetText("第{0}波 刷怪中", waveId);
            return;
        }

        _txtStage.SetText("--");
    }

    /// <summary>
    /// 设置波次阶段文本（无数据）。
    /// </summary>
    private void SetWaveStageText(string text)
    {
        if (_txtStage == null)
        {
            return;
        }

        _txtStage.SetText(text);
    }

    /// <summary>
    /// 设置波次倒计时文本。
    /// </summary>
    private void SetWaveCountdownText(int seconds)
    {
        if (_txtWaveCountdown == null)
        {
            return;
        }

        if (seconds < 0)
        {
            seconds = 0; // 负数保护
        }

        var minutes = seconds / 60; // 计算分钟
        var remainSeconds = seconds - minutes * 60; // 计算剩余秒
        _txtWaveCountdown.SetText("{0:00}:{1:00}", minutes, remainSeconds); // 按 mm:ss 输出
    }

    /// <summary>
    /// 设置波次倒计时文本（无数据）。
    /// </summary>
    private void SetWaveCountdownText(string text)
    {
        if (_txtWaveCountdown == null)
        {
            return;
        }

        _txtWaveCountdown.SetText(text);
    }
}
