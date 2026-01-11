using System.Collections;
using System.Collections.Generic;
using CYFramework; // CY.Resource 资源加载入口引用
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 人才物体
/// </summary>
public class GoTalents : MonoBehaviour
{
    /// <summary>
    /// 头像
    /// </summary>
    [SerializeField]
    private Image _imgHeadPortrait;
    /// <summary>
    /// 名称
    /// </summary>
    [SerializeField]
    private TextMeshProUGUI _txtName;
    /// <summary>
    /// 类别
    /// </summary>
    [SerializeField]
    private TextMeshProUGUI _txtType;
    /// <summary>
    /// 人才价格
    /// </summary>
    [SerializeField]
    private TextMeshProUGUI _txtRecruitmentFee;
    /// <summary>
    /// 招聘按钮（优先使用自身 Button 组件，避免运行时查找）。
    /// </summary>
    private Button _btnRecruit; // 招聘按钮缓存
    /// <summary>
    /// 当前条目绑定的员工配置 Id（Employee.csv 的 Id）。
    /// </summary>
    private int _employeeId; // 当前员工 Id 缓存
    /// <summary>
    /// 是否已绑定按钮点击事件（避免重复绑定）。
    /// </summary>
    private bool _hasBoundRecruitButton; // 按钮事件绑定标记

    /// <summary>
    /// Unity Awake：缓存按钮并绑定点击事件（低频）。
    /// </summary>
    private void Awake() // Awake 生命周期入口
    {
        CacheRecruitButton(); // 缓存招聘按钮组件
        BindRecruitButtonIfNeeded(); // 绑定招聘按钮点击事件
    }

    /// <summary>
    /// Unity OnDestroy：解绑点击事件，避免悬挂引用。
    /// </summary>
    private void OnDestroy() // 销毁回调入口
    {
        UnbindRecruitButton(); // 解绑招聘按钮点击事件
    }

    /// <summary>
    /// 设置人才显示数据。
    /// </summary>
    /// <param name="row">员工数据行（来自 Employee.csv）。</param>
    /// <param name="styleText">风格字符串（由 StyleIds 解析得到）。</param>
    public void SetData(EmployeeUnitRow row, string styleText) // 人才数据刷新入口
    {
        if (row == null) // 数据为空判定
        {
            _employeeId = 0; // 清空员工 Id，避免点击误触发
            SetNameText(string.Empty); // 清空名称显示
            SetTypeText(string.Empty); // 清空类型显示
            SetRecruitmentFeeText(string.Empty); // 清空价格显示
            SetHeadPortrait(string.Empty); // 清空头像显示
            return; // 数据为空时直接退出
        }

        _employeeId = row.Id; // 缓存员工 Id，用于点击招聘时派发事件
        SetNameText(row.Name); // 刷新名称显示
        SetTypeText(styleText); // 刷新风格/类型显示
        SetRecruitmentFeeText(row.RecruitmentPrice); // 刷新招聘价格显示
        SetHeadPortrait(row.IconPath); // 刷新头像显示（无兜底）
    }

    /// <summary>
    /// 缓存招聘按钮组件（优先使用当前物体上的 Button）。
    /// </summary>
    private void CacheRecruitButton() // 招聘按钮缓存入口
    {
        if (_btnRecruit != null) // 已缓存判定
        {
            return; // 已缓存时直接退出
        }

        _btnRecruit = GetComponent<Button>(); // 获取并缓存按钮组件（低频调用）
    }

    /// <summary>
    /// 绑定招聘按钮点击事件（只绑定一次）。
    /// </summary>
    private void BindRecruitButtonIfNeeded() // 招聘按钮绑定入口
    {
        if (_hasBoundRecruitButton) // 已绑定判定
        {
            return; // 已绑定时直接退出
        }

        if (_btnRecruit == null) // 按钮为空判定
        {
            return; // 按钮为空时直接退出
        }

        _btnRecruit.onClick.AddListener(OnRecruitButtonClicked); // 绑定点击回调
        _hasBoundRecruitButton = true; // 标记已绑定
    }

    /// <summary>
    /// 解绑招聘按钮点击事件。
    /// </summary>
    private void UnbindRecruitButton() // 招聘按钮解绑入口
    {
        if (!_hasBoundRecruitButton) // 未绑定判定
        {
            return; // 未绑定时直接退出
        }

        if (_btnRecruit == null) // 按钮为空判定
        {
            _hasBoundRecruitButton = false; // 按钮缺失时仍清理标记
            return; // 直接退出
        }

        _btnRecruit.onClick.RemoveListener(OnRecruitButtonClicked); // 解绑点击回调
        _hasBoundRecruitButton = false; // 清理绑定标记
    }

    /// <summary>
    /// 招聘按钮点击回调：派发“员工招聘请求”事件，由 CompanyEntity 负责生成员工。
    /// </summary>
    private void OnRecruitButtonClicked() // 招聘点击入口
    {
        if (_employeeId <= 0) // 员工 Id 无效判定
        {
            return; // 无效时直接退出
        }

        var evt = new EmployeeRecruitRequestedEvent // 创建招聘请求事件
        {
            EmployeeId = _employeeId // 写入员工配置 Id
        };
        CY.Event.Post(ref evt); // 派发事件（由玩法侧处理创建）
    }

    /// <summary>
    /// 设置头像图标（按路径加载 Sprite，加载失败不做兜底）。
    /// </summary>
    /// <param name="iconPath">Resources 相对路径（无扩展名）。</param>
    private void SetHeadPortrait(string iconPath) // 头像设置入口
    {
        if (_imgHeadPortrait == null) // 图片组件为空判定
        {
            return; // 图片组件为空时直接退出
        }

        if (string.IsNullOrEmpty(iconPath)) // 路径为空判定
        {
            _imgHeadPortrait.sprite = null; // 路径为空时清空头像
            return; // 直接退出
        }

        var sprite = CY.Resource.Load<Sprite>(iconPath); // 加载头像精灵（加载失败将返回 null）
        _imgHeadPortrait.sprite = sprite; // 赋值头像精灵（无兜底）
    }

    /// <summary>
    /// 设置名称文本。
    /// </summary>
    /// <param name="text">名称字符串。</param>
    private void SetNameText(string text) // 名称文本设置入口
    {
        if (_txtName == null) // 文本为空判定
        {
            return; // 文本为空时直接退出
        }

        text ??= string.Empty; // 文本为空时使用空字符串保护
        _txtName.SetText(text); // 写入名称文本
    }

    /// <summary>
    /// 设置类型/风格文本。
    /// </summary>
    /// <param name="text">类型字符串。</param>
    private void SetTypeText(string text) // 类型文本设置入口
    {
        if (_txtType == null) // 文本为空判定
        {
            return; // 文本为空时直接退出
        }

        text ??= string.Empty; // 文本为空时使用空字符串保护
        _txtType.SetText(text); // 写入类型文本
    }

    /// <summary>
    /// 设置招聘价格文本（字符串）。
    /// </summary>
    /// <param name="text">价格字符串。</param>
    private void SetRecruitmentFeeText(string text) // 价格文本设置入口
    {
        if (_txtRecruitmentFee == null) // 文本为空判定
        {
            return; // 文本为空时直接退出
        }

        text ??= string.Empty; // 文本为空时使用空字符串保护
        _txtRecruitmentFee.SetText(text); // 写入价格文本
    }

    /// <summary>
    /// 设置招聘价格文本（数值）。
    /// </summary>
    /// <param name="value">价格数值。</param>
    private void SetRecruitmentFeeText(int value) // 价格文本设置入口
    {
        if (_txtRecruitmentFee == null) // 文本为空判定
        {
            return; // 文本为空时直接退出
        }

        _txtRecruitmentFee.SetText("{0}", value); // 写入价格文本
    }
}
