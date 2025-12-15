using CYFramework;
using CYFramework.Core.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 加载界面（流程入口）
/// </summary>
/// <remarks>
/// 注意：关闭 UI 必须通过 UIManager（CY.UI / CloseSelf）进行，确保面板栈与对象池状态同步；
/// 禁止直接 Destroy(this/gameObject)，否则会导致 UIManager 仍认为该面板存在，后续打开/返回栈会错乱。
/// </remarks>
[UIPrefab("Prefabs/UI/Menu/Load/LoadUI")]
public class LoadUI : UIPanel
{
    /// <summary>
    /// 进入游戏按钮
    /// </summary>
    [SerializeField] private Button _btnEnterGame;

    protected override void OnBindUI()
    {
        base.OnBindUI();

        // 低频绑定：避免在 Update 中反复创建委托
        if (_btnEnterGame != null)
        {
            _btnEnterGame.onClick.AddListener(OnEnterGameClicked);
        }
    }

    protected override void OnUnbindUI()
    {
        if (_btnEnterGame != null)
        {
            _btnEnterGame.onClick.RemoveListener(OnEnterGameClicked);
        }

        base.OnUnbindUI();
    }

    /// <summary>
    /// 进入游戏
    /// </summary>
    private void OnEnterGameClicked()
    {
        // 先关闭加载界面，确保 UIManager 栈/对象池同步
        CloseSelf();

        // 再切换流程
        CY.Procedure.ChangeProcedure<MenuProcedure>();
    }
}
