// 云函数入口文件
const cloud = require('wx-server-sdk')

cloud.init({
  env: cloud.DYNAMIC_CURRENT_ENV
})

const db = cloud.database()

// 云函数入口函数
exports.main = async (event, context) => {
  const wxContext = cloud.getWXContext()
  const openid = wxContext.OPENID

  // 根据 action 分发不同操作
  switch (event.action) {
    case 'getUserData':
      return await getUserData(openid)
    case 'updateUserData':
      return await updateUserData(openid, event.data)
    case 'addScore':
      return await addScore(openid, event.score)
    case 'getScoreRank':
      return await getScoreRank(openid)
    default:
      // 默认获取玩家数据
      return await getUserData(openid)
  }
}

/**
 * 获取玩家数据，如果是新玩家则创建初始数据
 * @param {string} openid 玩家openid
 */
async function getUserData(openid) {
  try {
    // 查询玩家数据
    const result = await db.collection('user').where({
      _openid: openid
    }).get()

    if (result.data && result.data.length > 0) {
      // 老玩家，返回已有数据
      return {
        code: 0,
        msg: 'success',
        isNewPlayer: false,
        data: result.data[0]
      }
    } else {
      // 新玩家，创建初始数据
      const randomName = await getRandomDefaultName()
      const initialData = createInitialPlayerData(openid, randomName)

      // 写入数据库
      const addResult = await db.collection('user').add({
        data: initialData
      })

      // 返回初始数据
      return {
        code: 0,
        msg: 'success',
        isNewPlayer: true,
        data: {
          _id: addResult._id,
          ...initialData
        }
      }
    }
  } catch (error) {
    console.error('getUserData error:', error)
    return {
      code: -1,
      msg: error.message || 'unknown error',
      data: null
    }
  }
}

/**
 * 更新玩家数据
 * @param {string} openid 玩家openid
 * @param {object} data 要更新的数据
 */
async function updateUserData(openid, data) {
  try {
    const result = await db.collection('user').where({
      _openid: openid
    }).update({
      data: {
        ...data,
        updateTime: db.serverDate()
      }
    })

    return {
      code: 0,
      msg: 'success',
      updated: result.stats.updated
    }
  } catch (error) {
    console.error('updateUserData error:', error)
    return {
      code: -1,
      msg: error.message || 'unknown error'
    }
  }
}

/**
 * 增加玩家分数
 * @param {string} openid 玩家openid
 * @param {number} score 要增加的分数
 */
async function addScore(openid, score) {
  try {
    const _ = db.command
    const result = await db.collection('user').where({
      _openid: openid
    }).update({
      data: {
        Score: _.inc(score),
        updateTime: db.serverDate()
      }
    })

    return {
      code: 0,
      msg: 'success',
      updated: result.stats.updated
    }
  } catch (error) {
    console.error('addScore error:', error)
    return {
      code: -1,
      msg: error.message || 'unknown error'
    }
  }
}

/**
 * 获取分数排行榜（前50名）和当前玩家分数
 * @param {string} openid 玩家openid
 */
async function getScoreRank(openid) {
  try {
    // 获取前50名（按分数降序）
    const rankResult = await db.collection('user')
      .orderBy('Score', 'desc')
      .limit(50)
      .field({
        UserName: true,
        AvatarUri: true,
        Score: true
      })
      .get()

    // 获取当前玩家数据
    const myResult = await db.collection('user').where({
      _openid: openid
    }).field({
      UserName: true,
      AvatarUri: true,
      Score: true
    }).get()

    const myData = myResult.data && myResult.data.length > 0 ? myResult.data[0] : null

    // 计算当前玩家排名
    let myRank = -1
    if (myData) {
      const countResult = await db.collection('user').where({
        Score: db.command.gt(myData.Score)
      }).count()
      myRank = countResult.total + 1
    }

    return {
      code: 0,
      msg: 'success',
      data: {
        rankList: rankResult.data,
        myData: myData,
        myRank: myRank
      }
    }
  } catch (error) {
    console.error('getScoreRank error:', error)
    return {
      code: -1,
      msg: error.message || 'unknowns error',
      data: null
    }
  }
}

/**
 * 创建新玩家初始数据（字段名与 Unity BasePlayerData 保持一致，使用 Pascal 命名）
 * @param {string} openid 玩家openid
 * @param {string} defaultName 默认昵称
 */
function createInitialPlayerData(openid, defaultName) {
  return {
    _openid: openid,

    // 基础信息（与 BasePlayerData 字段名一致）
    UserName: defaultName || "新玩家",
    AvatarUri: "",

    // 货币
    Gold: 2500,

    // 积分
    Score: 0,

    // 卡包库存
    CardPacks: {
      RandomRarityPackCount: 0  // 初始随机卡包数量
    },

    // 稀有度卡片（初始为空）
    RarityCards: [],

    // 招募次数
    RecruitCount: 0,

    // 祈祷次数
    FreePrayCount: 3,

    // 蔬菜库存
    Vegetables: [
      {
        VegetableId: "vegetable_default",
        DisplayName: "基础蔬菜",
        Count: 0
      }
    ],

    // 消消乐道具库存
    Consumables: {
      PropKitCount: 0,      // 道具包数量
      ReviveItemCount: 0    // 复活道具数量
    },

    // 时间戳
    createTime: new Date(),
    updateTime: new Date()
  }
}

/**
 * 从 defaultNames 集合获取随机昵称
 */
async function getRandomDefaultName() {
  try {
    const result = await db.collection('defaultNames').limit(1).get()
    if (!result.data || result.data.length === 0) {
      return '新玩家'
    }

    const firstRecord = result.data[0]
    const names = firstRecord.names || firstRecord.defaultNames || []
    if (!Array.isArray(names) || names.length === 0) {
      return '新玩家'
    }

    const randomIndex = Math.floor(Math.random() * names.length)
    return names[randomIndex]
  } catch (error) {
    console.error('getRandomDefaultName error:', error)
    return '新玩家'
  }
}
