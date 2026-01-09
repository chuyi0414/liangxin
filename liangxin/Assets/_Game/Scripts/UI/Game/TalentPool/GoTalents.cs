using System.Collections;
using System.Collections.Generic;
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
}
