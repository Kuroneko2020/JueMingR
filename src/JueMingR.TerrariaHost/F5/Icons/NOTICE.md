# F5 顶部图标来源 / F5 tab icon provenance

本资源仅包含十二个固定图标的 alpha 遮罩。依据项目所有者在
[JueMingR Issue #30](https://github.com/Kuroneko2020/JueMingR/issues/30)
的七项视觉修正授权，从其 JueMingZ 项目自制基础几何图案离线派生。
没有复制 Legacy 运行时代码、纹理加载器或 UI 框架；无 Terraria、
TerrariaHelper、字体或第三方材质包资源。不对 Legacy 整仓授予新许可证，
也不将这些图标标称为第三方 MIT 图标集。

These twelve fixed alpha masks are derived offline from the project owner's
geometric JueMingZ artwork, with the owner's explicit authorization for this
JueMingR task. No Legacy runtime, game assets, fonts, or third-party skin assets
are included. This statement does not license the whole Legacy repository or
claim a third-party icon-set license.

源仓库 / Source: https://github.com/Kuroneko2020/JueMingZ

固定来源 / Source commit: `6ac6356c0c564f43284590c168855e96c50e13f7`

图形 / Geometry: `src/JueMingZ/UI/Legacy/Controls/LegacyVectorIconRenderer.cs`

源文件 SHA-256: `B40B5FC083C250FBF7E96C12BEFD6251A02F8D17934CCFEAD900DC84A1284A7D`

映射 / Mapping: `src/JueMingZ/UI/Legacy/LegacyTabBar.cs`

映射文件 SHA-256: `C3439D228CCDC5C4A900734BFA25656AA7F7AF447F8644F4B52658F8B7AE1E2D`

| 页签 / Tab | 图形 / Artwork ID |
| --- | --- |
| 物品 | item_bag |
| 杂项 | grid |
| 地图 | map_pin |
| 查询 | search |
| 笔记 | note |
| 关于 | info |
| 蓝图 | blueprint |
| 钓鱼 | fish |
| 战斗 | sword |
| 信息 | status_panel |
| 增益 | flask |
| 移动 | movement |

制作记录：`文档/更新记录/0371-ui-tab-icons.md`、`0372-ui-tab-icons-thin-strokes.md`、
`0374-ui-tab-icons-generated-and-short-labels.md` 记录用户要求纯代码图标及后续
几何遮罩制作。十个现存图案最早随 `5cac0ee08520443b90698e036760517d438b7dad`
进入 Git；物品袋来自 `6ec5512105555d85970cb581223a898ac2f9adb0`，笔记来自
`b253d5fe98e0e2dbeec32fc7e12abd1a213dacdd`。制作记录支持项目自制几何；
最初参考截图出处未记载，不声称已取得该截图或其其它内容的许可。

`tab-icons.geometry.json` 只记录这十二图案的数值几何，18×18 源坐标域。
`export-icons.py` 是独立离线导出工具，只用 Python 标准库；每个图元 5×5
子采样，图元覆盖率取 max，输出提高到 72×72 以支持较高 UI scale。
圆弧八段、椭圆四十段保持源图形轮廓。正常构建直接嵌入已提交的资产，
不运行 Python、不读取 Legacy，也不引入运行时解析依赖。

Asset format: twelve consecutive 72×72, row-major, 8-bit alpha masks in the
table order; 62,208 bytes. Runtime uploads premultiplied white pixels and fits
each visible source rectangle proportionally in the fixed logical icon box.

`tab-icons.alpha` SHA-256: `C367D0E155A22010CEDAC61CA62033AF9C33C6753D090D3043FF09A8774BBC4D`

本说明随 Host 程序集嵌入，图标及说明共同进入程序集和验证包身份。
This notice is embedded alongside the artwork in the Host assembly.
