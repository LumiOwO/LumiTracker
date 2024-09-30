# \[[v1.2.5](https://github.com/LumiOwO/LumiTracker/releases/tag/v1.2.5)\] - 2024.09.30

### 体验优化
- ${\color{#db3fff}{\textbf{[史诗]}}}$ 优化截帧测试按钮，并显示实时帧率，方便大家确认记牌功能是否正常启动
- ${\color{#db3fff}{\textbf{[史诗]}}}$ 新增牌组时，以角色的名称组合作为牌组的默认名称
- ${\color{#2a75e4}{\textbf{[稀有]}}}$ 支持网页版云原神
- ${\color{#2a75e4}{\textbf{[稀有]}}}$ OBS等录屏软件可以正常捕获到记牌器窗口了

### Bug修复
- ${\color{#ed8e06}{\textbf{[传说]}}}$ 可以正确处理「草与智慧」了
- ${\color{#db3fff}{\textbf{[史诗]}}}$ 修复了连续多次过牌时，第二次及以后的牌没有被记录到的问题
- ${\color{#2a75e4}{\textbf{[稀有]}}}$ 修复了卡牌识别失败后程序崩溃的问题
- ${\color{#2a75e4}{\textbf{[稀有]}}}$ 修复了窗口最小化后，恢复窗口大小时有概率闪退的问题

### 程序优化
- ${\color{#ed8e06}{\textbf{[传说]}}}$ 修复WindowsCapture截帧方式下，报错无堆栈信息导致无法定位出错位置的问题
- ${\color{#db3fff}{\textbf{[史诗]}}}$ 重构中心区域卡牌的检测逻辑，提升识别准确度

# \[[v1.2.4](https://github.com/LumiOwO/LumiTracker/releases/tag/v1.2.4)\] - 2024.09.18
### Bug修复
- ${\color{#2a75e4}{\textbf{[稀有]}}}$ 修复了金色的「骑士团图书馆」无法被识别的问题
- ${\color{#2a75e4}{\textbf{[稀有]}}}$ 修复了「幻戏倒计时」打出后记牌器界面崩溃的问题
- ${\color{#2a75e4}{\textbf{[稀有]}}}$ 进一步降低了「我方出牌」/「对方出牌」列表中出现不存在的卡牌的概率
### 程序优化
- ${\color{#db3fff}{\textbf{[史诗]}}}$ 添加了数字识别功能，并已用于回合数识别

# \[[v1.2.3](https://github.com/LumiOwO/LumiTracker/releases/tag/v1.2.3)\] - 2024.08.29
### Bug修复
- ${\color{#ed8e06}{\textbf{[传说]}}}$ 修复了特征提取算法中因窗口分辨率变化导致的Bug；该Bug曾导致金卡的识别准确率大幅下降
- ${\color{#db3fff}{\textbf{[史诗]}}}$ 修正了WindowsCapture截帧方式的边界偏移量错误
- **[普通]** 解决了导入包含新卡牌的分享码时程序闪退的问题
### 新增功能
- ${\color{#2a75e4}{\textbf{[稀有]}}}$ 添加5.0版本新卡
- ${\color{#2a75e4}{\textbf{[稀有]}}}$ 新增Beta版更新订阅选项；如果您希望帮助我们反馈bug，欢迎订阅~

# \[[v1.2.2](https://github.com/LumiOwO/LumiTracker/releases/tag/v1.2.2)\] - 2024.08.17
### 体验优化
- ${\color{#db3fff}{\textbf{[史诗]}}}$ 现在，卡牌数量变化时，会有高亮提示
- **[普通]** 优化主界面提示文本
### Bug修复
- ${\color{#ed8e06}{\textbf{[传说]}}}$ 新增**截帧方法**选项；无法启动对局记录的朋友，可以尝试更换截帧方法
- ${\color{#2a75e4}{\textbf{[稀有]}}}$ 修复BitBlt截屏方式下，游戏窗口退出时的资源释放问题
- **[普通]** 修复更新日志界面的文字颜色显示问题

# \[[v1.2.1](https://github.com/LumiOwO/LumiTracker/releases/tag/v1.2.1)\] - 2024.08.10
### Bug修复
- 修复启动架构更新导致的闪退问题

# \[[v1.2.0](https://github.com/LumiOwO/LumiTracker/releases/tag/v1.2.0)\] - 2024.08.10
### 体验优化
- **新增**: 支持自动更新，终于不用每次都卸载重装一遍了！

# \[[v1.1.0](https://github.com/LumiOwO/LumiTracker/releases/tag/v1.1.0)\] - 2024.08.03
### 检测功能
- **Add**: **过牌** 和 **向牌库新增卡牌**的检测
### 对局界面
- **Add**:  牌库剩余卡牌页面
### 启动界面
- **Add**:  牌库导入界面页面
### 体验优化
- **Add**:  配置文件跨版本保留（包括设置、牌组）
- **Add**:  优化主界面的关闭流程
- **Mod**: 调整Accent Color以及若干样式
### Bug修复
- **Temp**:  不区分`幻戏倒计时：3`的衍生牌

# \[[v1.0.3](https://github.com/LumiOwO/LumiTracker/releases/tag/v1.0.3)\] - 2024.07.20
- 增加4.8版本卡牌
- 新增选项：允许记牌器界面显示在游戏窗口外侧

# \[[v1.0.2](https://github.com/LumiOwO/LumiTracker/releases/tag/v1.0.2)\] - 2024.06.28
- 修复料理牌识别错误的问题
- 增加回合数记录功能
- 内置段位查询页面

# \[[v1.0.1](https://github.com/LumiOwO/LumiTracker/releases/tag/v1.0.1)\] - 2024.06.14
- 修复部分Win10的闪退问题
- 增强卡面识别的鲁棒性
- 支持云原神
- 支持 16:10 和 21:9 的画面比例
- 补充「幻戏倒计时：3」的衍生牌

# \[[v1.0.0](https://github.com/LumiOwO/LumiTracker/releases/tag/v1.0.0)\] - 2024.06.07
- 初始版本发布
- 支持记录己方/对方打出的牌
