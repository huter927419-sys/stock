using System;
using MQReceiver.Configuration;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 新6个计算条件的通用计算器条件类
    /// 全域：M1=78, M2=65, M3=50, M4=30, N=5（默认）
    /// A1=MAX(K2,K3), A2=MIN(K2,K3), A3=K1>D1且K2>D2且K3>D3, A4=K2>D2且K3>D3, A5=K1>D1
    /// 统一：昨天成交金额>=N*1亿
    /// </summary>
    public class NewFilterCondition
    {
        public int FilterId { get; set; }
        public string Name { get; set; }
        public decimal M1 { get; set; }
        public decimal M2 { get; set; }
        public decimal M3 { get; set; }
        public decimal M4 { get; set; }
        public int N { get; set; }

        public NewFilterCondition(int filterId)
        {
            FilterId = filterId;
            var config = AppConfigProvider.Instance;
            M1 = config.GetDecimal("GlobalThreshold_M1", 78m);
            M2 = config.GetDecimal("GlobalThreshold_M2", 65m);
            M3 = config.GetDecimal("GlobalThreshold_M3", 50m);
            M4 = config.GetDecimal("GlobalThreshold_M4", 30m);
            N = config.GetInt("GlobalThreshold_N", 5);
            Name = config.GetString($"Filter{filterId}_Name", $"计算{filterId}");
        }

        /// <summary>
        /// 构造函数（允许自定义M1/M2/M3/M4/N值）
        /// </summary>
        public NewFilterCondition(int filterId, decimal m1, decimal m2, decimal m3, decimal m4, int n)
        {
            FilterId = filterId;
            M1 = m1;
            M2 = m2;
            M3 = m3;
            M4 = m4;
            N = n;
            var config = AppConfigProvider.Instance;
            Name = config.GetString($"Filter{filterId}_Name", $"计算{filterId}");
        }

        /// <summary>
        /// 检查是否满足计算条件
        /// K1=周K, K2=月K, K3=季K ; D1=周D, D2=月D, D3=季D
        /// A1=MAX(K2,K3), A2=MIN(K2,K3), A3=K1>D1且K2>D2且K3>D3, A4=K2>D2且K3>D3, A5=K1>D1
        /// </summary>
        public bool CheckCondition(decimal k1, decimal k2, decimal k3, decimal d1, decimal d2, decimal d3)
        {
            decimal a1 = Math.Max(k2, k3);
            decimal a2 = Math.Min(k2, k3);
            bool a3 = k1 > d1 && k2 > d2 && k3 > d3;
            bool a4 = k2 > d2 && k3 > d3;
            bool a5 = k1 > d1;

            switch (FilterId)
            {
                case 1: // 强多排列：K1>A1 AND A2>=M1 AND A3
                    return k1 > a1 && a2 >= M1 && a3;

                case 2: // 中多排列：K1>A1 AND A2>=M2 AND A2<M1 AND A3
                    return k1 > a1 && a2 >= M2 && a2 < M1 && a3;

                case 3: // 强多缠绕：K1>M3 AND K1<A1 AND A2>=M1 AND A4
                    return k1 > M3 && k1 < a1 && a2 >= M1 && a4;

                case 4: // 中多缠绕：K1>M3 AND K1<A1 AND A2>=M2 AND A2<M1 AND A4
                    return k1 > M3 && k1 < a1 && a2 >= M2 && a2 < M1 && a4;

                case 5: // 强多反弹：K1<A2 AND A2>=M1 AND K1<M3
                    return k1 < a2 && a2 >= M1 && k1 < M3;

                case 6: // 中多反弹：K1<A2 AND A2>=M2 AND A2<M1 AND K1<M4
                    return k1 < a2 && a2 >= M2 && a2 < M1 && k1 < M4;

                default:
                    return false;
            }
        }
    }
}
