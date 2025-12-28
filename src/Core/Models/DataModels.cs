using System;

namespace MQReceiver
{
    // 向后兼容 - 数据模型已移至 MQReceiver.Models 命名空间
    // 新代码建议使用 MQReceiver.Models.DailyDataRecord 等

    /// <summary>
    /// 日线数据记录（向后兼容）
    /// </summary>
    public class DailyDataRecord : Models.DailyDataRecord
    {
    }

    /// <summary>
    /// 除权数据记录（向后兼容）
    /// </summary>
    public class ExRightsDataRecord : Models.ExRightsDataRecord
    {
    }

    /// <summary>
    /// 实时数据记录（向后兼容）
    /// </summary>
    public class RealTimeDataRecord : Models.RealTimeDataRecord
    {
    }
}
