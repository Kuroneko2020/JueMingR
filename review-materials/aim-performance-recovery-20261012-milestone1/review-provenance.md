# 首轮独审原件及后继整合边界

`first-independent-review-original.md`仅提取独审会话最终assistant消息，完整保留意见/期待原文；SHA256 `7478EC54DB26A38B2655CF2C02F1EB8EA85EBEBE790FE5F0D1177C7F465A7876`。来源thread `01a12f80-6775-7e53-81b9-b0fbf5caea34`，源session标识`rollout-2026-10-12T17-45-06-01a12f80-6775-7e53-81b9-b0fbf5caea34.jsonl`；只选择response_item/assistant/phase=final_answer且起始句匹配的唯一output_text。没有上传整个session、推理、工具或命令历史。结论NEEDS_FIX/INCOMPLETE保留，整合writer不改独审意见，也不替代增量复审。

后继已改对应Basic正规入口P2：Basic纳NativeCombatAimChecks默认链及Usage的basic-aim专项，仍需最终固定候选/包实际运行后才闭此覆盖门。其他持续/OFF/b038/冷启动/预算/无关风/自然Draw门不因源码改动而关闭。

`natural-g05-r11/`保存复用Render/costCanvas+单Color[]全像素读回后的有效g05原件。standing290/294（idle2..4预算后段值7.298/6.5211/6.3263ms、attack180一相容撤回），moving294/294；birth15/15、hits16/15，geometry/landing/movement等原分母保留。原source420个cs来自380132e固定归档，仅NaturalImpact/ProbeGraphics有实际增量，另ImpactVisual复制件与380相同；源码差异/provenance及原日志/CSV/输入facts/帧图均保留。

重要修正：后来确认g05的HostCombatObservation完成阶段使用Runtime私有updateTick，原生callback使用Main.GameUpdateCount，同一真实世界更新可重置6ms owner。g05 CSV预算值仅表示后段，不可当“整帧未耗尽”的证据；整段外部Stopwatch总loop与phase计量仍有各自有效范围。新g07统一真实GUC的反例与持续成本验证待取得；不能据旧false直接断言首shot非预算。r12/r13的Harmony `object[] __args`对out struct写回default改变原生consumer，所有入口/gate/因果/数值全部排除，原排除说明保留。r14改为不接触out参数后600geometry相等但30出生有第二shot一差异，只能支持本run首shot局部/独立静态clock接缝，不能代签r11完整等效或性能。

g06内部ref传递无超噪声收益已撤，不是第二性能里程碑。g07是独立私有载荷（Host SHA0E12FC88C66F3A12689B0ACF545E3E2726962364EF07FCBB68D7B02E44446D5A、MVIDacb6c341-68a1-4369-b81a-f257372320f1），含GUC统一、地形全干区域证明、实际风依赖收窄。它通过干区正确性反控，但同景性能、动态受风反控和最终独审仍待验证，不能沿用g05成绩给它准入。
