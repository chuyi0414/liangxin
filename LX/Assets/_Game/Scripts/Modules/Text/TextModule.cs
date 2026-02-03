using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// 文本模块组件（示例：按键触发一次逻辑）。
/// </summary>
public class TextModule : GameFrameworkComponent
{
    /// <summary>
    /// Unity 帧更新入口（用于检测一次性按键触发）。
    /// </summary>
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            HandleQPressed();
        }
    }

    /// <summary>
    /// 按下 Q 键时触发的逻辑入口（单次触发）。
    /// </summary>
    private void HandleQPressed()
    {
        // TODO: 在此处实现按键触发后的具体逻辑。
        for(int i =1;i<=5;i++)
        {
            GameEntry.GameManager.TryCreationEnemy("0",new Vector3(10,0 ,0));
        }
    }
}
