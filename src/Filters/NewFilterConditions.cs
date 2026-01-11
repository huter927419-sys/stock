using System;
using MQReceiver.Configuration;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 新8个过滤条件的通用过滤器条件类
    /// </summary>
    public class NewFilterCondition
    {
        public int FilterId { get; set; }
        public string Name { get; set; }
        public decimal M1 { get; set; }
        public decimal M2 { get; set; }
        public decimal M3 { get; set; }

        public NewFilterCondition(int filterId)
        {
            FilterId = filterId;
            var config = AppConfigProvider.Instance;
            M1 = config.GetDecimal("GlobalThreshold_M1", 78m);
            M2 = config.GetDecimal("GlobalThreshold_M2", 65m);
            M3 = config.GetDecimal("GlobalThreshold_M3", 50m);
            Name = config.GetString($"Filter{filterId}_Name", $"过滤{filterId}");
        }

        /// <summary>
        /// 构造函数（允许自定义M1/M2/M3值）
        /// </summary>
        public NewFilterCondition(int filterId, decimal m1, decimal m2, decimal m3)
        {
            FilterId = filterId;
            M1 = m1;
            M2 = m2;
            M3 = m3;
            var config = AppConfigProvider.Instance;
            Name = config.GetString($"Filter{filterId}_Name", $"过滤{filterId}");
        }

        /// <summary>
        /// 检查是否满足过滤条件
        /// K1=周K, K2=月K, K3=季K
        /// RefK1=昨日周K, RefK2=昨日月K
        /// </summary>
        public bool CheckCondition(decimal k1, decimal k2, decimal k3, decimal refK1, decimal refK2)
        {
            switch (FilterId)
            {
                case 1: // 强多金叉：K1>K2 AND K2>K3 AND K3>=M1 AND REF(K1,1)<REF(K2,1)
                    return k1 > k2 && k2 > k3 && k3 >= M1 && refK1 < refK2;

                case 2: // 中多金叉：K1>K2 AND K2>K3 AND K3>=M2 AND K3<M1 AND REF(K1,1)<REF(K2,1)
                    return k1 > k2 && k2 > k3 && k3 >= M2 && k3 < M1 && refK1 < refK2;

                case 3: // 强多排列：K1>K2 AND K2>K3 AND K3>=M1 AND REF(K1,1)>REF(K2,1)
                    return k1 > k2 && k2 > k3 && k3 >= M1 && refK1 > refK2;

                case 4: // 中多排列：K1>K2 AND K2>K3 AND K3>=M2 AND K3<M1 AND REF(K1,1)>REF(K2,1)
                    return k1 > k2 && k2 > k3 && k3 >= M2 && k3 < M1 && refK1 > refK2;

                case 5: // 强多缠绕：(K2<K3 AND K1>K3 AND K2>=M1) OR (K1>K3 AND K2>K3 AND K1<K2 AND K3>=M1)
                    return (k2 < k3 && k1 > k3 && k2 >= M1) ||
                           (k1 > k3 && k2 > k3 && k1 < k2 && k3 >= M1);

                case 6: // 中多缠绕：(K2<K3 AND K1>K3 AND K2>=M2 AND K2<M1) OR (K1>K3 AND K2>K3 AND K1<K2 AND K3>=M2 AND K3<M1)
                    return (k2 < k3 && k1 > k3 && k2 >= M2 && k2 < M1) ||
                           (k1 > k3 && k2 > k3 && k1 < k2 && k3 >= M2 && k3 < M1);

                case 7: // 强多反弹：(K3>=M1 AND K2>K3 AND K1<M3) OR (K2>=M1 AND K2<K3 AND K1<M3)
                    return (k3 >= M1 && k2 > k3 && k1 < M3) ||
                           (k2 >= M1 && k2 < k3 && k1 < M3);

                case 8: // 中多反弹：(K3>=M2 AND K3<M1 AND K2>K3 AND K1<M3) OR (K2>=M2 AND K2<M1 AND K2<K3 AND K1<M3)
                    return (k3 >= M2 && k3 < M1 && k2 > k3 && k1 < M3) ||
                           (k2 >= M2 && k2 < M1 && k2 < k3 && k1 < M3);

                default:
                    return false;
            }
        }
    }
}
