# AIPredictive 分级预警（GradingAlert）

对方原版算法脚本 + 模型；**切片由上位机完成，不改 py**。

## 当前默认

| 配置 | 含义 | 值 |
|------|------|-----|
| `IntervalMinutes` | 多久分析一次 | **120（2 小时）** |
| `LookbackHours` | 每次喂多少数据 | **2 小时** |

即：每 2 小时跑一轮，每轮用最近 2 小时 CSV 切片。

## 文件

| 文件 | 作用 |
|------|------|
| `V8.0_model.json` | 冻结基线（对方交付，只读） |
| `分级预警推理.py` | 对方原版推理（只读） |
| `grading_config.json` | 上位机调度配置 |

## 上位机每轮

1. 抽出最近 `LookbackHours` 行 → 覆盖 `_grading_state\input_slice.csv`  
2. 调 `分级预警推理.py`  
3. 无新时间戳跳过；有一级/二级即弹红遮罩（确认仅关窗，不抑制）  

## 依赖

```bat
pip install pandas numpy
```
