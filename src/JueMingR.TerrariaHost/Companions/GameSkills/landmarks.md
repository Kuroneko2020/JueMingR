## 按存档找到地标和材料

“我脚下”指消息发送时的主人位置，“你脚下”指队友当前位置，“当前选点”指实际选择的标记或参照。用 `observe_game` 的 `observe_destinations` 查标记与地点，用 `find_world_memory` 和 `read_world_memory` 查存档里的共享档案。地标可由不同队友复用，现场方块、液体和物品引用仍应由负责行动的队友重新观察。手写位置是转述。要记实际地点，使用 `observe_tiles` 的目标给 `write_world_memory`，更新时使用当前修订。用 `query_world_materials` 查附近地块及覆盖。多人尚未加载的区块不能当成没有材料。去采集前问清地点、材料、数量和收尾，到场重新确认。
