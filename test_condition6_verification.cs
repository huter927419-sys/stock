using System;

namespace MQReceiver.Tests
{
    /// <summary>
    /// 条件6（金叉）逻辑验证和测试
    /// 公式：A1=MAX(K2,K3), REF(K1) < REF(A1,1) AND K1 > A1
    /// </summary>
    public class Condition6Verification
    {
        /// <summary>
        /// 验证条件6的逻辑
        /// </summary>
        public static void VerifyCondition6()
        {
            Console.WriteLine("=== 条件6（金叉）逻辑验证 ===\n");
            Console.WriteLine("公式：A1=MAX(K2,K3), REF(K1) < REF(A1,1) AND K1 > A1");
            Console.WriteLine("说明：A1取K2（月K）和K3（季K）中较大的值\n");

            // 测试用例1：月K > 季K 的情况
            Console.WriteLine("【测试用例1：月K > 季K】");
            TestCase(
                k1: 70m,      // 今日周K
                k2: 80m,      // 今日月K
                k3: 60m,      // 今日季K
                refK1: 50m,   // 昨日周K
                refK2: 75m,   // 昨日月K
                refK3: 55m,   // 昨日季K
                expected: true,
                description: "月K > 季K，昨日周K < 昨日月K，今日周K > 今日月K（金叉）"
            );

            // 测试用例2：季K > 月K 的情况
            Console.WriteLine("\n【测试用例2：季K > 月K】");
            TestCase(
                k1: 75m,      // 今日周K
                k2: 60m,      // 今日月K
                k3: 80m,      // 今日季K
                refK1: 50m,   // 昨日周K
                refK2: 55m,   // 昨日月K
                refK3: 75m,   // 昨日季K
                expected: true,
                description: "季K > 月K，昨日周K < 昨日季K，今日周K > 今日季K（金叉）"
            );

            // 测试用例3：不满足条件（昨日周K >= 昨日A1）
            Console.WriteLine("\n【测试用例3：不满足条件 - 昨日周K >= 昨日A1】");
            TestCase(
                k1: 70m,      // 今日周K
                k2: 80m,      // 今日月K
                k3: 60m,      // 今日季K
                refK1: 80m,   // 昨日周K（>= 昨日月K，不满足 REF(K1) < REF(A1,1)）
                refK2: 75m,   // 昨日月K
                refK3: 55m,   // 昨日季K
                expected: false,
                description: "昨日周K >= 昨日月K，不满足 REF(K1) < REF(A1,1)"
            );

            // 测试用例4：不满足条件（今日周K <= 今日A1）
            Console.WriteLine("\n【测试用例4：不满足条件 - 今日周K <= 今日A1】");
            TestCase(
                k1: 70m,      // 今日周K（<= 今日月K，不满足 K1 > A1）
                k2: 80m,      // 今日月K
                k3: 60m,      // 今日季K
                refK1: 50m,   // 昨日周K
                refK2: 75m,   // 昨日月K
                refK3: 55m,   // 昨日季K
                expected: false,
                description: "今日周K <= 今日月K，不满足 K1 > A1"
            );

            // 测试用例5：边界情况（相等）
            Console.WriteLine("\n【测试用例5：边界情况 - 相等】");
            TestCase(
                k1: 80m,      // 今日周K（= 今日月K，不满足 K1 > A1）
                k2: 80m,      // 今日月K
                k3: 60m,      // 今日季K
                refK1: 50m,   // 昨日周K
                refK2: 75m,   // 昨日月K
                refK3: 55m,   // 昨日季K
                expected: false,
                description: "今日周K = 今日月K，不满足 K1 > A1（需要严格大于）"
            );

            // 测试用例6：实际金叉场景
            Console.WriteLine("\n【测试用例6：实际金叉场景】");
            TestCase(
                k1: 72m,      // 今日周K（上穿月K）
                k2: 70m,      // 今日月K
                k3: 65m,      // 今日季K
                refK1: 68m,   // 昨日周K（< 昨日月K）
                refK2: 71m,   // 昨日月K
                refK3: 64m,   // 昨日季K
                expected: true,
                description: "周K从68上穿到72，超过月K（70），形成金叉"
            );

            Console.WriteLine("\n=== 验证完成 ===");
        }

        /// <summary>
        /// 执行单个测试用例
        /// </summary>
        private static void TestCase(
            decimal k1, decimal k2, decimal k3,
            decimal refK1, decimal refK2, decimal refK3,
            bool expected, string description)
        {
            // 计算 A1 和 REF(A1, 1)
            decimal a1 = Math.Max(k2, k3);           // A1 = MAX(月K, 季K)
            decimal refA1 = Math.Max(refK2, refK3);   // REF(A1,1) = MAX(昨日月K, 昨日季K)

            // 检查条件
            bool condition1 = refK1 < refA1;  // REF(K1) < REF(A1,1)
            bool condition2 = k1 > a1;        // K1 > A1
            bool result = condition1 && condition2;

            // 显示详细信息
            Console.WriteLine($"描述: {description}");
            Console.WriteLine($"  今日: 周K={k1}, 月K={k2}, 季K={k3}");
            Console.WriteLine($"  昨日: 周K={refK1}, 月K={refK2}, 季K={refK3}");
            Console.WriteLine($"  A1 = MAX(月K, 季K) = MAX({k2}, {k3}) = {a1}");
            Console.WriteLine($"  REF(A1,1) = MAX(昨日月K, 昨日季K) = MAX({refK2}, {refK3}) = {refA1}");
            Console.WriteLine($"  条件1: REF(K1) < REF(A1,1) → {refK1} < {refA1} = {condition1}");
            Console.WriteLine($"  条件2: K1 > A1 → {k1} > {a1} = {condition2}");
            Console.WriteLine($"  结果: {condition1} AND {condition2} = {result}");
            Console.WriteLine($"  预期: {expected}");
            
            if (result == expected)
            {
                Console.WriteLine($"  ✓ 测试通过");
            }
            else
            {
                Console.WriteLine($"  ✗ 测试失败！预期 {expected}，实际 {result}");
            }
        }

        /// <summary>
        /// 主函数（用于独立测试）
        /// </summary>
        public static void Main(string[] args)
        {
            VerifyCondition6();
            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }
    }
}
