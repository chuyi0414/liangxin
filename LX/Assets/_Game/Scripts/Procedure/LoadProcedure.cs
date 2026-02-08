using GameFramework.DataTable;
using GameFramework.Event;
using GameFramework.Fsm;
using GameFramework.Procedure;
using UnityGameFramework.Runtime;

/// <summary>
/// 加载流程
/// </summary>
public class LoadProcedure : ProcedureBase
{
    //总加载数量
    private int _loadNumber = 3;
    //已经加载的数量
    private int _accomplishLoadNumber = 0;


    //LoadUIForm表Id
    private int _loadUIFormId;

    //DRBattleData
    private DataTableBase _dRBattleData;
    //DRProtagonist
    private DataTableBase _dRProtagonist;
    //DRProjectile
    private DataTableBase _dRProjectile;
    //DREnemy
    private DataTableBase _dREnemy;
    //DREmployee
    private DataTableBase _dREmployee;

    protected override void OnInit(IFsm<IProcedureManager> procedureOwner)
    {
        base.OnInit(procedureOwner);
        

    }
    protected override void OnEnter(IFsm<IProcedureManager> procedureOwner)
    {
        base.OnEnter(procedureOwner);

        GameEntry.Event.Subscribe(LoadDataTableSuccessEventArgs.EventId, OnLoadDataTableSuccess);
        GameEntry.Event.Subscribe(LoadDataTableFailureEventArgs.EventId, OnLoadDataTableFailure);
        //战斗数据
        if (GameEntry.DataTable.HasDataTable<DRBattleData>())
        {
            _dRBattleData = (DataTableBase)GameEntry.DataTable.GetDataTable<DRBattleData>();
        }
        else
        {
            _dRBattleData = (DataTableBase)GameEntry.DataTable.CreateDataTable<DRBattleData>();
        }
        _dRBattleData.ReadData("DataTables/Game/BattleData"
            , new object[]
            {
                this,
                "BattleData"
            });
        //主角
        if (GameEntry.DataTable.HasDataTable<DRProtagonist>())
        {
            _dRProtagonist = (DataTableBase)GameEntry.DataTable.GetDataTable<DRProtagonist>();
        }
        else
        {
            _dRProtagonist = (DataTableBase)GameEntry.DataTable.CreateDataTable<DRProtagonist>();
        }
        _dRProtagonist.ReadData("DataTables/Entity/Unit/Protagonist/Protagonist"
            , new object[]
            {
                this,
                "Protagonist"
            });
        //子弹
        if (GameEntry.DataTable.HasDataTable<DRProjectile>())
        {
            _dRProjectile = (DataTableBase)GameEntry.DataTable.GetDataTable<DRProjectile>();
        }
        else
        {
            _dRProjectile = (DataTableBase)GameEntry.DataTable.CreateDataTable<DRProjectile>();
        }
        _dRProjectile.ReadData("DataTables/Entity/Projectile/Projectile"
            , new object[]
            {
                this,
                "Projectile"
            });
        //敌人
        if (GameEntry.DataTable.HasDataTable<DREnemy>())
        {
            _dREnemy = (DataTableBase)GameEntry.DataTable.GetDataTable<DREnemy>();
        }
        else
        {
            _dREnemy = (DataTableBase)GameEntry.DataTable.CreateDataTable<DREnemy>();
        }
        _dREnemy.ReadData("DataTables/Entity/Unit/Enemy/Enemy"
            , new object[]
            {
                this,
                "Enemy"
            });
        //员工
        if (GameEntry.DataTable.HasDataTable<DREmployee>())
        {
            _dREmployee = (DataTableBase)GameEntry.DataTable.GetDataTable<DREmployee>();
        }
        else
        {
            _dREmployee = (DataTableBase)GameEntry.DataTable.CreateDataTable<DREmployee>();
        }
        _dREmployee.ReadData("DataTables/Entity/Unit/Employee/Employee"
            ,new object[]
             {
                 this,
                "Employee"
             });
    }

    protected override void OnUpdate(IFsm<IProcedureManager> procedureOwner, float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);

    }

    protected override void OnLeave(IFsm<IProcedureManager> procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);

        GameEntry.Event.Unsubscribe(LoadDataTableSuccessEventArgs.EventId, OnLoadDataTableSuccess);
        GameEntry.Event.Unsubscribe(LoadDataTableFailureEventArgs.EventId, OnLoadDataTableFailure);

        if(_loadUIFormId != 0)
        GameEntry.UI.CloseUIForm(_loadUIFormId);
    }

    protected override void OnDestroy(IFsm<IProcedureManager> procedureOwner)
    {
        base.OnDestroy(procedureOwner);

    }

    private void OnLoadDataTableFailure(object sender, GameEventArgs e)
    {

    }

    private void OnLoadDataTableSuccess(object sender, GameEventArgs e)
    {
        LoadDataTableSuccessEventArgs ne = e as LoadDataTableSuccessEventArgs;
        
        object[] os = ne.UserData as object[];
        if (os[0] != this)
            return;

        if (os[1].Equals("BattleData"))
        {
            IDataTable<DRBattleData> dRBattleDatas = GameEntry.DataTable.GetDataTable<DRBattleData>();
            GameEntry.GameManager.DRBattleDatas = dRBattleDatas;
        }
        else if (os[1].Equals("Protagonist"))
        {
            IDataTable<DRProtagonist> dRProtagonists = GameEntry.DataTable.GetDataTable<DRProtagonist>();
            GameEntry.GameManager.DRProtagonists = dRProtagonists;
        }
        else if (os[1].Equals("Projectile"))
        {
            IDataTable<DRProjectile> dRProjectiles = GameEntry.DataTable.GetDataTable<DRProjectile>();
            GameEntry.GameManager.DRProjectiles = dRProjectiles;
        }
        else if (os[1].Equals("Enemy"))
        {
            IDataTable<DREnemy> dREnemys = GameEntry.DataTable.GetDataTable<DREnemy>();
            GameEntry.GameManager.DREnemies = dREnemys;
        }
        else if (os[1].Equals("Employee"))
        {
            IDataTable<DREmployee> dDREmployees = GameEntry.DataTable.GetDataTable<DREmployee>();
            GameEntry.GameManager.DREmployees = dDREmployees;
        }

            _accomplishLoadNumber++;
        if(_accomplishLoadNumber == _loadNumber)
        {
            _loadUIFormId = GameEntry.UI.OpenUIForm("Prefabs/UI/Load/LoadUIForm", "Normal");
        }
    }
}
