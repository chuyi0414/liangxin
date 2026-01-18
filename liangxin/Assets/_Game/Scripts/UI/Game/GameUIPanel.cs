using System; // Array 等基础类型引用
using System.Collections.Generic; // IReadOnlyList 等集合接口引用
using System.Text; // StringBuilder 引用
using System.Globalization; // 数字格式化引用
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
    [Header("玩家")]
    /// <summary>
    /// 玩家Icon
    /// </summary>
    [SerializeField] private Image _imgPlayer;
    /// <summary>
    /// 玩家血条
    /// </summary>
    [SerializeField] private Slider _sliderPlayer;
    /// <summary>
    /// 玩家血条文本
    /// </summary>
    [SerializeField] private TMP_Text _txtHealthBarPlayer;
    /// <summary>
    /// 玩家头像是否已加载
    /// </summary>
    private bool _playerIconLoaded; // 玩家头像加载标记
    /// <summary>
    /// 玩家名称
    /// </summary>
    [SerializeField] private TMP_Text _txtPlayerName;
    /// <summary>
    /// 玩家等级
    /// </summary>
    [SerializeField] private TMP_Text _txtPlayerLevel;
    /// <summary>
    /// 玩家攻击力
    /// </summary>
    [SerializeField] private TMP_Text _txtPlayerAttack;
    /// <summary>
    /// 玩家防御力
    /// </summary>
    [SerializeField] private TMP_Text _txtPlayerDefense;
    /// <summary>
    /// 玩家固定防御穿透值
    /// </summary>
    [SerializeField] private TMP_Text _txtPlayerDefensePenetration;
    /// <summary>
    /// 玩家百分比防御穿透
    /// </summary>
    [SerializeField] private TMP_Text _txtPlayerDefensePenetrationRate;
    /// <summary>
    /// 玩家暴击率
    /// </summary>
    [SerializeField] private TMP_Text _txtPlayerCritRate;
    /// <summary>
    /// 玩家暴击率倍率
    /// </summary>
    [SerializeField] private TMP_Text _txtPlayerCritMultiplier;
    /// <summary>
    /// 玩家闪避率
    /// </summary>
    [SerializeField] private TMP_Text _txtPlayerDodgeRate;
    /// <summary>
    /// 玩家近战图
    /// </summary>
    [SerializeField] private Image _imagePlayerCloseCombat;
    /// <summary>
    /// 玩家远程图
    /// </summary>
    [SerializeField] private Image _imagePlayerRemote;

    [Header("员工")]
    /// <summary>
    /// 员工Icon
    /// </summary>
    [SerializeField] private Image _imgEmployee;
    /// <summary>
    /// 员工血条
    /// </summary>
    [SerializeField] private Slider _sliderEmployee;
    /// <summary>
    /// 员工血条文本
    /// </summary>
    [SerializeField] private TMP_Text _txtHealthBarEmployee;
    /// <summary>
    /// 员工风格
    /// </summary>
    [SerializeField] private TMP_Text _txtEmployeeStyle;
    /// <summary>
    /// 员工名称
    /// </summary>
    [SerializeField] private TMP_Text _txtEmployeeName;
    /// <summary>
    /// 员工等级
    /// </summary>
    [SerializeField] private TMP_Text _txtEmployeeLevel;
    /// <summary>
    /// 员工攻击力
    /// </summary>
    [SerializeField] private TMP_Text _txtEmployeeAttack;
    /// <summary>
    /// 员工防御力
    /// </summary>
    [SerializeField] private TMP_Text _txtEmployeeDefense;
    /// <summary>
    /// 员工固定防御穿透值
    /// </summary>
    [SerializeField] private TMP_Text _txtEmployeeDefensePenetration;
    /// <summary>
    /// 员工百分比防御穿透
    /// </summary>
    [SerializeField] private TMP_Text _txtEmployeeDefensePenetrationRate;
    /// <summary>
    /// 员工暴击率
    /// </summary>
    [SerializeField] private TMP_Text _txtEmployeeCritRate;
    /// <summary>
    /// 员工暴击率倍率
    /// </summary>
    [SerializeField] private TMP_Text _txtEmployeeCritMultiplier;
    /// <summary>
    /// 员工闪避率
    /// </summary>
    [SerializeField] private TMP_Text _txtEmployeeDodgeRate;
    /// <summary>
    /// 未选中员工Go
    /// </summary>
    [SerializeField] private GameObject _goMask;
    /// <summary>
    /// 员工近战图
    /// </summary>
    [SerializeField] private Image _imageEmployeeCloseCombat;
    /// <summary>
    /// 员工远程图
    /// </summary>
    [SerializeField] private Image _imageEmployeeRemote;

    /// <summary>波次 UI 刷新计时器。</summary>
    private Timer _waveUiTimer;
    /// <summary>是否已订阅战斗数据事件。</summary>
    private bool _battleDataEventsSubscribed; // 战斗数据事件订阅标记
    /// <summary>
    /// 当前选中的员工单位。
    /// </summary>
    private UnitEntity _selectedEmployee; // 当前选中员工单位缓存

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
    /// 人才库刷新
    /// </summary>
    [SerializeField] private Button _btnRefreshTalentPool;
    /// <summary>
    /// 人才库刷新价格
    /// </summary>
    [SerializeField] private TextMeshProUGUI _txtRefreshTalentPool;
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
    /// 招聘配置表名常量。
    /// </summary>
    private const string RecruitConfigTableName = "RecruitConfig"; // 招聘配置表名
    /// <summary>
    /// 招聘平台表名常量。
    /// </summary>
    private const string RecruitPlatformTableName = "RecruitPlatform"; // 招聘平台表名
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
        if (_btnRefreshTalentPool != null)
        {
            _btnRefreshTalentPool.onClick.AddListener(OnBtnRefreshTalentPoolClick); // 绑定人才库刷新按钮事件
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

        if (!TryGetRecruitConfigRow(out var recruitConfigRow)) // 获取招聘配置行
        {
            HideTalentPoolItems(items, slotCount); // 配置不可用时隐藏所有条目
            return; // 直接退出
        }

        if (!TryGetRecruitPlatformRows(out var recruitPlatformRows, out var recruitPlatformCount)) // 获取招聘平台行列表
        {
            HideTalentPoolItems(items, slotCount); // 平台不可用时隐藏所有条目
            return; // 直接退出
        }

        var showCount = Mathf.Min(clampedTarget, employeeCount, slotCount); // 计算最终显示数量（不足则按已有数量显示）
        if (showCount <= 0) // 最终显示数量判定
        {
            HideTalentPoolItems(items, slotCount); // 无需显示时隐藏所有条目
            return; // 直接退出
        }

        for (int i = 0; i < slotCount; i++) // 遍历人才库槽位
        {
            var item = items[i]; // 获取当前槽位脚本
            if (i >= showCount) // 超出显示数量判定
            {
                SetTalentItemActive(item, false, i); // 隐藏多余条目
                continue; // 继续下一个槽位
            }

            var employeeIndex = UnityEngine.Random.Range(0, employeeCount); // 随机获取员工索引（允许重复）
            var employeeRow = employeeRows[employeeIndex]; // 获取员工数据行
            if (employeeRow == null) // 员工行为空判定
            {
                SetTalentItemActive(item, false, i); // 员工行为空时隐藏条目
                continue; // 继续下一个槽位
            }

            var styleText = BuildEmployeeStyleText(employeeRow); // 将 StyleIds 解析为风格字符串
            var recruitType = PickRecruitType(recruitConfigRow); // 随机招聘类型
            var platformName = PickRecruitPlatformName(recruitPlatformRows, recruitPlatformCount); // 随机招聘平台名称
            var recruitWaveCount = GetRecruitWaveCount(recruitType, recruitConfigRow); // 获取招聘波数
            var recruitmentPrice = RecruitTypeUtility.CalculatePrice(employeeRow.RecruitmentPrice, recruitType); // 计算最终招聘价格
            if (item != null) // 脚本存在判定
            {
                item.SetData(employeeRow, styleText, platformName, recruitType, recruitWaveCount, recruitmentPrice); // 刷新条目显示
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
    /// 获取招聘配置数据行（RecruitConfig.csv）。
    /// </summary>
    /// <param name="row">输出配置行。</param>
    /// <returns>是否获取成功。</returns>
    private bool TryGetRecruitConfigRow(out RecruitConfigRow row) // 招聘配置数据获取入口
    {
        row = null; // 默认输出为空
        var dataManager = CY.Data; // 获取数据表管理器
        if (dataManager == null) // 管理器为空判定
        {
            CY.LogWarning("[GameUIPanel] DataTableManager 未就绪，无法读取招聘配置。"); // 输出警告日志
            return false; // 管理器为空时返回失败
        }

        if (!dataManager.HasDataTable(RecruitConfigTableName)) // 配置表未加载判定
        {
            CY.LogWarning("[GameUIPanel] 招聘配置表未加载，无法读取招聘配置。"); // 输出警告日志
            return false; // 未加载时返回失败
        }

        var table = dataManager.GetDataTable<RecruitConfigRow>(RecruitConfigTableName); // 获取招聘配置表实例
        if (table == null) // 表实例为空判定
        {
            CY.LogWarning("[GameUIPanel] 招聘配置表为空，无法读取招聘配置。"); // 输出警告日志
            return false; // 表为空时返回失败
        }

        var rows = table.GetAllRows(); // 获取所有配置行
        if (rows == null || rows.Count <= 0) // 行列表为空判定
        {
            CY.LogWarning("[GameUIPanel] 招聘配置表无有效行，无法读取招聘配置。"); // 输出警告日志
            return false; // 无有效行时返回失败
        }

        row = rows[0]; // 读取第一行配置
        if (row == null) // 行为空判定
        {
            CY.LogWarning("[GameUIPanel] 招聘配置行为空，无法读取招聘配置。"); // 输出警告日志
            return false; // 行为空时返回失败
        }

        if (row.UrgentWeight < 0f || row.NormalWeight < 0f || row.TempWeight < 0f) // 权重为负判定
        {
            CY.LogError("[GameUIPanel] 招聘配置权重为负，无法读取招聘配置。"); // 输出错误日志
            return false; // 权重无效时返回失败
        }

        var totalWeight = row.UrgentWeight + row.NormalWeight + row.TempWeight; // 计算权重总和
        if (totalWeight <= 0f) // 权重总和无效判定
        {
            CY.LogError("[GameUIPanel] 招聘配置权重总和为 0，无法读取招聘配置。"); // 输出错误日志
            return false; // 总和无效时返回失败
        }

        if (row.NormalWaveMin < 1 || row.NormalWaveMax < row.NormalWaveMin) // 普通波数范围非法判定
        {
            CY.LogError("[GameUIPanel] 普通招聘波数范围非法，无法读取招聘配置。"); // 输出错误日志
            return false; // 范围非法时返回失败
        }

        if (row.TempWaveMin < 1 || row.TempWaveMax < row.TempWaveMin) // 临时工波数范围非法判定
        {
            CY.LogError("[GameUIPanel] 临时工波数范围非法，无法读取招聘配置。"); // 输出错误日志
            return false; // 范围非法时返回失败
        }

        return true; // 返回获取成功
    }

    /// <summary>
    /// 获取招聘平台数据行列表（RecruitPlatform.csv）。
    /// </summary>
    /// <param name="rows">输出平台行列表。</param>
    /// <param name="count">输出平台数量。</param>
    /// <returns>是否获取成功。</returns>
    private bool TryGetRecruitPlatformRows(out IReadOnlyList<RecruitPlatformRow> rows, out int count) // 招聘平台数据获取入口
    {
        rows = null; // 默认输出为空
        count = 0; // 默认输出数量为 0

        var dataManager = CY.Data; // 获取数据表管理器
        if (dataManager == null) // 管理器为空判定
        {
            CY.LogWarning("[GameUIPanel] DataTableManager 未就绪，无法读取招聘平台。"); // 输出警告日志
            return false; // 管理器为空时返回失败
        }

        if (!dataManager.HasDataTable(RecruitPlatformTableName)) // 平台表未加载判定
        {
            CY.LogWarning("[GameUIPanel] 招聘平台表未加载，无法读取招聘平台。"); // 输出警告日志
            return false; // 未加载时返回失败
        }

        var table = dataManager.GetDataTable<RecruitPlatformRow>(RecruitPlatformTableName); // 获取招聘平台表实例
        if (table == null) // 表实例为空判定
        {
            CY.LogWarning("[GameUIPanel] 招聘平台表为空，无法读取招聘平台。"); // 输出警告日志
            return false; // 表为空时返回失败
        }

        rows = table.GetAllRows(); // 获取所有平台行
        count = rows != null ? rows.Count : 0; // 获取平台数量
        if (count <= 0) // 平台数量为空判定
        {
            CY.LogWarning("[GameUIPanel] 招聘平台表无有效行，无法读取招聘平台。"); // 输出警告日志
            return false; // 无有效行时返回失败
        }

        return true; // 返回获取成功
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
    /// 随机获取招聘类型（按权重）。
    /// </summary>
    /// <param name="configRow">招聘配置行。</param>
    /// <returns>招聘类型。</returns>
    private RecruitType PickRecruitType(RecruitConfigRow configRow) // 招聘类型随机入口
    {
        var urgentWeight = configRow.UrgentWeight; // 读取急聘权重
        var normalWeight = configRow.NormalWeight; // 读取普通权重
        var tempWeight = configRow.TempWeight; // 读取临时工权重
        var totalWeight = urgentWeight + normalWeight + tempWeight; // 计算权重总和
        var roll = UnityEngine.Random.value * totalWeight; // 按权重总和随机取值
        if (roll < urgentWeight) // 急聘命中判定
        {
            return RecruitType.Urgent; // 返回急聘
        }

        roll -= urgentWeight; // 去掉急聘权重区间
        if (roll < normalWeight) // 普通招聘命中判定
        {
            return RecruitType.Normal; // 返回普通招聘
        }

        return RecruitType.Temp; // 返回临时工
    }

    /// <summary>
    /// 随机获取招聘平台名称。
    /// </summary>
    /// <param name="rows">平台数据行列表。</param>
    /// <param name="count">平台数量。</param>
    /// <returns>平台名称。</returns>
    private string PickRecruitPlatformName(IReadOnlyList<RecruitPlatformRow> rows, int count) // 招聘平台随机入口
    {
        if (rows == null || count <= 0) // 列表为空判定
        {
            return string.Empty; // 列表为空时返回空字符串
        }

        for (int attempt = 0; attempt < count; attempt++) // 尝试按次数随机挑选
        {
            var index = UnityEngine.Random.Range(0, count); // 获取随机索引
            var row = rows[index]; // 获取平台行
            if (row == null) // 行为空判定
            {
                continue; // 行为空时跳过
            }

            var name = row.Name; // 读取平台名称
            if (string.IsNullOrEmpty(name)) // 名称为空判定
            {
                continue; // 名称为空时跳过
            }

            return name; // 返回有效平台名称
        }

        CY.LogWarning("[GameUIPanel] 招聘平台表无有效名称，无法显示平台。"); // 输出缺失日志
        return string.Empty; // 未找到有效名称时返回空字符串
    }

    /// <summary>
    /// 获取招聘波数（按招聘类型）。
    /// </summary>
    /// <param name="recruitType">招聘类型。</param>
    /// <param name="configRow">招聘配置行。</param>
    /// <returns>招聘波数。</returns>
    private int GetRecruitWaveCount(RecruitType recruitType, RecruitConfigRow configRow) // 招聘波数获取入口
    {
        if (recruitType == RecruitType.Urgent) // 急聘判定
        {
            return 0; // 急聘立即刷新
        }

        if (recruitType == RecruitType.Normal) // 普通招聘判定
        {
            return UnityEngine.Random.Range(configRow.NormalWaveMin, configRow.NormalWaveMax + 1); // 返回普通招聘等待波数
        }

        return UnityEngine.Random.Range(configRow.TempWaveMin, configRow.TempWaveMax + 1); // 返回临时工持续波数
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
    /// 人才库刷新按钮点击回调。
    /// </summary>
    private void OnBtnRefreshTalentPoolClick() // 人才库刷新按钮点击入口
    {
        RefreshTalentPoolContent(); // 点击刷新时重新生成人才库内容
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
        RefreshPlayerHud(); // 刷新玩家头像与血量显示
        RefreshBattleData();
        SetEmployeeMaskActive(true); // 初始未选中员工时显示遮罩
        StartWaveUiTimer();
    }

    /// <summary>
    /// 面板刷新时同步显示
    /// </summary>
    protected override void OnRefresh(object userData)
    {
        base.OnRefresh(userData);
        RefreshBattleData();
        RefreshPlayerHud(); // 刷新玩家头像与血量显示
    }

    /// <summary>
    /// 面板隐藏时重置人才库位置。
    /// </summary>
    protected override void OnHide() // 面板隐藏回调入口
    {
        base.OnHide(); // 调用父类隐藏回调
        ResetTalentPoolToHidden(); // 隐藏时重置人才库位置
    }

    /// <summary>
    /// 面板回收时重置人才库状态。
    /// </summary>
    protected override void OnRecycle() // 面板回收回调入口
    {
        ResetTalentPoolToHidden(); // 回收时重置人才库位置与展开状态
        base.OnRecycle(); // 调用父类回收回调
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
        if (_btnRefreshTalentPool != null)
        {
            _btnRefreshTalentPool.onClick.RemoveListener(OnBtnRefreshTalentPoolClick); // 解绑人才库刷新按钮事件
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
        CY.Event.Subscribe<UnitHpChangedEvent>(OnUnitHpChanged, this); // 订阅单位血量变化事件
        CY.Event.Subscribe<EmployeeSelectedEvent>(OnEmployeeSelected, this); // 订阅员工选中事件
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
    /// 单位血量变化事件回调。
    /// </summary>
    /// <param name="evt">血量变化事件。</param>
    private void OnUnitHpChanged(ref UnitHpChangedEvent evt) // 单位血量事件回调入口
    {
        if (evt.Unit == null) // 单位为空判定
        {
            return; // 单位为空时退出
        }

        if (evt.Unit.Camp == UnitCamp.Player) // 玩家单位判定
        {
            EnsurePlayerIconLoaded(); // 确保玩家头像已加载
            SetPlayerHealth(evt.CurrentHp, evt.MaxHp); // 刷新玩家血量显示
            SetPlayerInfo(evt.Unit); // 刷新玩家基础信息
            return; // 玩家处理完成后退出
        }

        if (_selectedEmployee == null) // 未选中员工判定
        {
            return; // 未选中时不刷新员工血量
        }

        if (evt.Unit != _selectedEmployee) // 非当前选中员工判定
        {
            return; // 非选中员工不处理
        }

        SetEmployeeHealth(evt.CurrentHp, evt.MaxHp); // 刷新选中员工血量显示
    }

    /// <summary>
    /// 员工选中事件回调。
    /// </summary>
    /// <param name="evt">员工选中事件。</param>
    private void OnEmployeeSelected(ref EmployeeSelectedEvent evt) // 员工选中事件回调入口
    {
        var selectedEmployee = evt.IsSelected ? evt.Employee : null; // 获取当前选中员工
        _selectedEmployee = selectedEmployee; // 缓存当前选中员工

        if (selectedEmployee == null) // 未选中员工判定
        {
            SetEmployeeMaskActive(true); // 未选中时显示遮罩
            return; // 未选中时直接退出
        }

        SetEmployeeMaskActive(false); // 选中员工时隐藏遮罩
        RefreshSelectedEmployeeHud(selectedEmployee); // 刷新选中员工显示
    }

    /// <summary>
    /// 刷新玩家头像与血量显示（从单位管理器读取）。
    /// </summary>
    private void RefreshPlayerHud() // 玩家 UI 刷新入口
    {
        EnsurePlayerIconLoaded(); // 确保玩家头像已加载
        var unitManager = CY.Unit; // 获取单位管理器
        if (unitManager == null) // 管理器为空判定
        {
            ResetPlayerHud(); // 管理器为空时重置玩家显示
            return; // 管理器为空时退出
        }

        var player = unitManager.Player; // 获取玩家实体
        if (player == null) // 玩家为空判定
        {
            ResetPlayerHud(); // 玩家为空时重置玩家显示
            return; // 玩家为空时退出
        }

        SetPlayerHealth(player.CurrentHp, player.MaxHp); // 刷新玩家血量显示
        SetPlayerInfo(player); // 刷新玩家基础信息
    }

    /// <summary>
    /// 确保玩家头像已加载（从 Player.csv 读取 IconPath）。
    /// </summary>
    private void EnsurePlayerIconLoaded() // 玩家头像加载入口
    {
        if (_playerIconLoaded) // 已加载判定
        {
            return; // 已加载时直接退出
        }

        if (_imgPlayer == null) // 头像组件为空判定
        {
            _playerIconLoaded = true; // 头像组件缺失时停止重复加载
            return; // 组件缺失时退出
        }

        var unitManager = CY.Unit; // 获取单位管理器
        if (unitManager == null) // 管理器为空判定
        {
            return; // 管理器为空时退出（保留重试机会）
        }

        if (!unitManager.TryGetDefaultPlayerRow(out var row) || row == null) // 玩家数据行无效判定
        {
            return; // 数据行无效时退出（保留重试机会）
        }

        var iconPath = row.IconPath; // 读取玩家头像路径
        if (string.IsNullOrEmpty(iconPath)) // 路径为空判定
        {
            _imgPlayer.sprite = null; // 清空头像显示
            _playerIconLoaded = true; // 标记已处理头像
            return; // 路径为空时退出
        }

        var sprite = CY.Resource.Load<Sprite>(iconPath); // 按路径加载头像精灵
        _imgPlayer.sprite = sprite; // 写入头像精灵
        _playerIconLoaded = true; // 标记已加载头像
    }

    /// <summary>
    /// 设置玩家血量显示（滑动条与文本）。
    /// </summary>
    /// <param name="currentHp">当前生命值。</param>
    /// <param name="maxHp">最大生命值。</param>
    private void SetPlayerHealth(int currentHp, int maxHp) // 玩家血量显示入口
    {
        SetPlayerHealthSlider(currentHp, maxHp); // 刷新玩家血条滑动条
        SetPlayerHealthText(currentHp, maxHp); // 刷新玩家血条文本
    }

    /// <summary>
    /// 刷新玩家血条滑动条。
    /// </summary>
    /// <param name="currentHp">当前生命值。</param>
    /// <param name="maxHp">最大生命值。</param>
    private void SetPlayerHealthSlider(int currentHp, int maxHp) // 玩家血条滑动条刷新入口
    {
        if (_sliderPlayer == null) // 滑动条为空判定
        {
            return; // 滑动条为空时退出
        }

        if (maxHp <= 0) // 最大生命无效判定
        {
            _sliderPlayer.value = 0f; // 最大生命无效时清空进度
            return; // 最大生命无效时退出
        }

        var ratio = (float)currentHp / maxHp; // 计算血量比例
        _sliderPlayer.value = Mathf.Clamp01(ratio); // 写入滑动条值
    }

    /// <summary>
    /// 刷新玩家血条文本。
    /// </summary>
    /// <param name="currentHp">当前生命值。</param>
    /// <param name="maxHp">最大生命值。</param>
    private void SetPlayerHealthText(int currentHp, int maxHp) // 玩家血条文本刷新入口
    {
        if (_txtHealthBarPlayer == null) // 文本为空判定
        {
            return; // 文本为空时退出
        }

        if (maxHp <= 0) // 最大生命无效判定
        {
            _txtHealthBarPlayer.SetText("--"); // 最大生命无效时显示占位
            return; // 最大生命无效时退出
        }

        _txtHealthBarPlayer.SetText("{0}/{1}", currentHp, maxHp); // 写入血量文本
    }

    /// <summary>
    /// 重置玩家显示为默认占位。
    /// </summary>
    private void ResetPlayerHud() // 玩家显示重置入口
    {
        SetPlayerHealth(0, 0); // 重置玩家血量显示
        SetValueText(_txtPlayerName, "--"); // 清空玩家名称
        SetValueText(_txtPlayerLevel, "--"); // 清空玩家等级
        SetValueText(_txtPlayerAttack, "--"); // 清空玩家攻击力
        SetValueText(_txtPlayerDefense, "--"); // 清空玩家防御力
        SetValueText(_txtPlayerDefensePenetration, "--"); // 清空玩家固定穿透
        SetValueText(_txtPlayerDefensePenetrationRate, "--"); // 清空玩家百分比穿透
        SetValueText(_txtPlayerCritRate, "--"); // 清空玩家暴击率
        SetValueText(_txtPlayerCritMultiplier, "--"); // 清空玩家暴击倍率
        SetValueText(_txtPlayerDodgeRate, "--"); // 清空玩家闪避率
    }

    /// <summary>
    /// 刷新玩家基础属性文本。
    /// </summary>
    /// <param name="player">玩家单位。</param>
    private void SetPlayerInfo(UnitEntity player) // 玩家基础信息刷新入口
    {
        if (player == null) // 玩家为空判定
        {
            return; // 玩家为空时直接退出
        }

        var stats = player.BaseStats; // 读取玩家基础属性
        SetValueText(_txtPlayerName, player.UnitName); // 刷新玩家名称
        SetValueText(_txtPlayerLevel, player.Level); // 刷新玩家等级
        SetValueText(_txtPlayerAttack, stats.Attack); // 刷新玩家攻击力
        SetValueText(_txtPlayerDefense, stats.Defense); // 刷新玩家防御力
        SetValueText(_txtPlayerDefensePenetration, stats.DefensePenetration); // 刷新玩家固定穿透
        SetRateText(_txtPlayerDefensePenetrationRate, stats.DefensePenetrationRate); // 刷新玩家百分比穿透
        SetRateText(_txtPlayerCritRate, stats.CritRate); // 刷新玩家暴击率
        SetFloatText(_txtPlayerCritMultiplier, stats.CritMultiplier); // 刷新玩家暴击倍率
        SetRateText(_txtPlayerDodgeRate, stats.DodgeRate); // 刷新玩家闪避率
        RefreshPlayerCombatIcon(stats.IsRanged); // 刷新玩家近战/远程图标
    }

    /// <summary>
    /// 刷新玩家近战/远程图标显示。
    /// </summary>
    /// <param name="isRanged">是否远程单位。</param>
    private void RefreshPlayerCombatIcon(bool isRanged) // 玩家近战/远程图标刷新入口
    {
        if (_imagePlayerCloseCombat == null) // 近战图标为空判定
        {
            CY.LogWarning("[GameUIPanel] 玩家近战图标未绑定。"); // 输出图标缺失警告
            return; // 近战图标为空时退出
        }

        if (_imagePlayerRemote == null) // 远程图标为空判定
        {
            CY.LogWarning("[GameUIPanel] 玩家远程图标未绑定。"); // 输出图标缺失警告
            return; // 远程图标为空时退出
        }

        _imagePlayerCloseCombat.gameObject.SetActive(!isRanged); // 近战时显示近战图标
        _imagePlayerRemote.gameObject.SetActive(isRanged); // 远程时显示远程图标
    }

    /// <summary>
    /// 设置员工遮罩显示/隐藏。
    /// </summary>
    /// <param name="isActive">是否显示。</param>
    private void SetEmployeeMaskActive(bool isActive) // 员工遮罩显隐入口
    {
        if (_goMask == null) // 遮罩对象为空判定
        {
            return; // 遮罩对象为空时直接退出
        }

        _goMask.SetActive(isActive); // 设置遮罩显示状态
    }

    /// <summary>
    /// 刷新选中员工显示。
    /// </summary>
    /// <param name="employee">选中员工单位。</param>
    private void RefreshSelectedEmployeeHud(UnitEntity employee) // 选中员工显示刷新入口
    {
        if (employee == null) // 员工为空判定
        {
            return; // 员工为空时直接退出
        }

        var unitManager = CY.Unit; // 获取单位管理器
        if (unitManager == null) // 管理器为空判定
        {
            CY.LogWarning("[GameUIPanel] UnitManager 未就绪，无法刷新选中员工。"); // 输出缺失警告
            return; // 管理器为空时退出
        }

        if (!unitManager.TryGetEmployeeRow(employee.UnitConfigId, out var row) || row == null) // 员工数据行获取判定
        {
            CY.LogWarning($"[GameUIPanel] 未找到员工数据行，Id={employee.UnitConfigId}"); // 输出缺失警告
            return; // 数据行缺失时退出
        }

        var stats = employee.BaseStats; // 读取员工基础属性
        SetEmployeeHealth(employee.CurrentHp, employee.MaxHp); // 刷新员工血量显示
        SetValueText(_txtEmployeeName, employee.UnitName); // 刷新员工名称
        SetValueText(_txtEmployeeLevel, employee.Level); // 刷新员工等级
        SetValueText(_txtEmployeeAttack, stats.Attack); // 刷新员工攻击力
        SetValueText(_txtEmployeeDefense, stats.Defense); // 刷新员工防御力
        SetValueText(_txtEmployeeDefensePenetration, stats.DefensePenetration); // 刷新员工固定穿透
        SetRateText(_txtEmployeeDefensePenetrationRate, stats.DefensePenetrationRate); // 刷新员工百分比穿透
        SetRateText(_txtEmployeeCritRate, stats.CritRate); // 刷新员工暴击率
        SetFloatText(_txtEmployeeCritMultiplier, stats.CritMultiplier); // 刷新员工暴击倍率
        SetRateText(_txtEmployeeDodgeRate, stats.DodgeRate); // 刷新员工闪避率
        RefreshEmployeeCombatIcon(stats.IsRanged); // 刷新员工近战/远程图标

        var styleText = BuildEmployeeStyleText(row); // 构建员工风格文本
        SetValueText(_txtEmployeeStyle, styleText); // 刷新员工风格显示
        SetEmployeeIcon(row.IconPath); // 刷新员工头像显示
    }

    /// <summary>
    /// 刷新员工近战/远程图标显示。
    /// </summary>
    /// <param name="isRanged">是否远程单位。</param>
    private void RefreshEmployeeCombatIcon(bool isRanged) // 员工近战/远程图标刷新入口
    {
        if (_imageEmployeeCloseCombat == null) // 近战图标为空判定
        {
            CY.LogWarning("[GameUIPanel] 员工近战图标未绑定。"); // 输出图标缺失警告
            return; // 近战图标为空时退出
        }

        if (_imageEmployeeRemote == null) // 远程图标为空判定
        {
            CY.LogWarning("[GameUIPanel] 员工远程图标未绑定。"); // 输出图标缺失警告
            return; // 远程图标为空时退出
        }

        _imageEmployeeCloseCombat.gameObject.SetActive(!isRanged); // 近战时显示近战图标
        _imageEmployeeRemote.gameObject.SetActive(isRanged); // 远程时显示远程图标
    }

    /// <summary>
    /// 设置员工头像显示。
    /// </summary>
    /// <param name="iconPath">Resources 相对路径。</param>
    private void SetEmployeeIcon(string iconPath) // 员工头像设置入口
    {
        if (_imgEmployee == null) // 头像组件为空判定
        {
            return; // 头像组件为空时退出
        }

        if (string.IsNullOrEmpty(iconPath)) // 路径为空判定
        {
            _imgEmployee.sprite = null; // 路径为空时清空头像
            return; // 路径为空时退出
        }

        var sprite = CY.Resource.Load<Sprite>(iconPath); // 按路径加载头像精灵
        _imgEmployee.sprite = sprite; // 写入头像精灵
    }

    /// <summary>
    /// 设置员工血量显示（滑动条与文本）。
    /// </summary>
    /// <param name="currentHp">当前生命值。</param>
    /// <param name="maxHp">最大生命值。</param>
    private void SetEmployeeHealth(int currentHp, int maxHp) // 员工血量显示入口
    {
        SetEmployeeHealthSlider(currentHp, maxHp); // 刷新员工血条滑动条
        SetEmployeeHealthText(currentHp, maxHp); // 刷新员工血条文本
    }

    /// <summary>
    /// 刷新员工血条滑动条。
    /// </summary>
    /// <param name="currentHp">当前生命值。</param>
    /// <param name="maxHp">最大生命值。</param>
    private void SetEmployeeHealthSlider(int currentHp, int maxHp) // 员工血条滑动条刷新入口
    {
        if (_sliderEmployee == null) // 滑动条为空判定
        {
            return; // 滑动条为空时退出
        }

        if (maxHp <= 0) // 最大生命无效判定
        {
            _sliderEmployee.value = 0f; // 最大生命无效时清空进度
            return; // 最大生命无效时退出
        }

        var ratio = (float)currentHp / maxHp; // 计算血量比例
        _sliderEmployee.value = Mathf.Clamp01(ratio); // 写入滑动条值
    }

    /// <summary>
    /// 刷新员工血条文本。
    /// </summary>
    /// <param name="currentHp">当前生命值。</param>
    /// <param name="maxHp">最大生命值。</param>
    private void SetEmployeeHealthText(int currentHp, int maxHp) // 员工血条文本刷新入口
    {
        if (_txtHealthBarEmployee == null) // 文本为空判定
        {
            return; // 文本为空时退出
        }

        if (maxHp <= 0) // 最大生命无效判定
        {
            _txtHealthBarEmployee.SetText("--"); // 最大生命无效时显示占位
            return; // 最大生命无效时退出
        }

        _txtHealthBarEmployee.SetText("{0}/{1}", currentHp, maxHp); // 写入血量文本
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
            SetValueText(_txtRefreshTalentPool, "--"); // 刷新价格未就绪时显示占位
            SetCompanyPollutionScrollbar(0);
            return;
        }

        SetValueText(_txtMoney, manager.MoneyCurrent); // 刷新资金显示
        SetValueText(_txtConscience, manager.ConscienceCurrent); // 刷新良心显示
        SetValueText(_txtBlackHeart, manager.BlackHeartCurrent); // 刷新黑心显示
        var companyConscience = manager.CompanyConscienceCurrent; // 读取公司良心当前值
        var companyPollution = manager.CompanyPollutionCurrent; // 读取公司污染当前值
        var companyPollutionMax = data.CompanyPollution; // 读取公司污染阈值
        SetValueText(_txtRefreshTalentPool, data.TalentPoolRefreshPrice); // 刷新人才库刷新价格

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
    /// 设置浮点数文本（最多保留两位小数）。
    /// </summary>
    /// <param name="target">目标文本。</param>
    /// <param name="value">浮点值。</param>
    private static void SetFloatText(TMP_Text target, float value) // 浮点文本设置入口
    {
        if (target == null) // 目标为空判定
        {
            return; // 目标为空时直接退出
        }

        var formattedValue = value.ToString("0.##", CultureInfo.InvariantCulture); // 按格式生成浮点字符串
        target.SetText(formattedValue); // 写入浮点文本
    }

    /// <summary>
    /// 设置比例文本（0-1 转百分比）。
    /// </summary>
    /// <param name="target">目标文本。</param>
    /// <param name="rate">比例值（0-1）。</param>
    private static void SetRateText(TMP_Text target, float rate) // 比例文本设置入口
    {
        if (target == null) // 目标为空判定
        {
            return; // 目标为空时直接退出
        }

        var percent = Mathf.RoundToInt(Mathf.Clamp01(rate) * 100f); // 计算百分比整数
        target.SetText("{0}%", percent); // 写入百分比文本
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

        if (!waveManager.TryGetMainWaveStatus(out var waveId, out var stage, out var remaining)) // 获取显示波次编号与阶段数据
        {
            SetWaveStageText("--");
            SetWaveCountdownText("--:--");
            return;
        }

        SetWaveStageText(waveManager.CurrentWaveCount, stage); // 刷新波次阶段显示
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
