using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

/// <summary>
/// 人才卡片显示组件。
/// 负责接收外部设置的人才数据，并通过数据绑定更新姓名文本。
/// </summary>
public class GoTalents : MonoBehaviour
{
    /// <summary>
    /// 当前卡片绑定的人才数据 Id。
    /// </summary>
    public int Id;

    /// <summary>
    /// 姓名文本组件。
    /// 由预制体在 Inspector 中拖拽赋值。
    /// </summary>
    [SerializeField]
    public TextMeshProUGUI TxtName;

    /// <summary>
    /// 当前卡片姓名绑定使用的唯一键。
    /// </summary>
    private string _nameKey;

    /// <summary>
    /// 招募人才
    /// </summary>
    [SerializeField]
    private Button _btnRecruitTalents;

    private void Start()
    {
        _btnRecruitTalents.onClick.AddListener(OnBtnRecruitTalentsClick);
    }

    /// <summary>
    /// 销毁时解绑当前组件上的所有数据绑定，防止回调泄漏。
    /// </summary>
    private void OnDestroy()
    {
        GameEntry.DataBinding.UnbindAll(this);
        _nameKey = null;
    }

    /// <summary>
    /// 招募人才
    /// </summary>
    private void OnBtnRecruitTalentsClick()
    {
        GameEntry.GameManager.CreateEmployee(Id,GameEntry.GameManager._defaultMap._employeeGenericPoint.position);
    }

    /// <summary>
    /// 初始化姓名数据绑定。
    /// 每个卡片应使用不同 index，避免多个卡片共享同一个键。
    /// </summary>
    /// <param name="index">卡片槽位索引。</param>
    public void InitNameBinding(int index)
    {
        if (!string.IsNullOrWhiteSpace(_nameKey))
        {
            GameEntry.DataBinding.Unbind(this, _nameKey);
        }

        _nameKey = $"GoTalents{GetInstanceID()}_{index}";

        GameEntry.DataBinding.Bind<string>(
            _nameKey,
            string.Empty,
            OnNameValueChanged,
            this
        );
    }

    /// <summary>
    /// 通过数据绑定设置姓名。
    /// </summary>
    /// <param name="nameValue">要显示的姓名。</param>
    public void SetName(string nameValue)
    {
        if (string.IsNullOrWhiteSpace(_nameKey))
        {
            return;
        }

        GameEntry.DataBinding.Set<string>(_nameKey, nameValue ?? string.Empty);
    }

    /// <summary>
    /// 姓名绑定值变化回调。
    /// </summary>
    /// <param name="nameValue">最新姓名值。</param>
    private void OnNameValueChanged(string nameValue)
    {
        if (TxtName == null)
        {
            return;
        }

        TxtName.text = nameValue ?? string.Empty;
    }
}
