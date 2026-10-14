# -*- coding: utf-8 -*-
"""
V8.0 分级预警推理.py —— 在冻结 C1/C2 之上新增风险分级决策模块。
======================================================================
分级依据（可解释、可追溯，不简单按 C1+C2=一级）：
  C1强度分 = min(离群比, 5)，离群比 = 最长高负载run / 正常run_P99，>2 才计分
  C2强度分 = min(max(|独立活动率|/3, |独立有效扭矩|/15), 5)，>1 才计分
  风险分 R = max(C1分, C2分) + 0.5 × min(C1分, C2分)
  分级：一级 R>=3 | 二级 1.5<=R<3 | 三级 0<R<1.5 | 正常 R==0
  无法判断：训练活动率<1% 或 有效扭矩基线缺失
等级表示「巡检优先级」，不是未来故障概率。
======================================================================
"""
import argparse, json, os, glob, warnings
import numpy as np
import pandas as pd

warnings.filterwarnings("ignore")


def load_model(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def load_data(path):
    """读单个 CSV 或整个文件夹（递归读取所有 .csv 并合并、去重、按时间排序）。"""
    if os.path.isdir(path):
        files = sorted(glob.glob(os.path.join(path, "**", "*.csv"), recursive=True))
        if not files:
            raise FileNotFoundError(f"文件夹里没有找到任何 CSV 文件：{path}")
        frames = [pd.read_csv(f, encoding="utf-8-sig", low_memory=False) for f in files]
        d = pd.concat(frames, ignore_index=True)
    else:
        d = pd.read_csv(path, encoding="utf-8-sig", low_memory=False)
    if "Timestamp" not in d.columns:
        raise ValueError("数据缺少 Timestamp 列")
    d["Timestamp"] = pd.to_datetime(d["Timestamp"])
    d = d.drop_duplicates(subset="Timestamp").sort_values("Timestamp").reset_index(drop=True)
    return d


def run_lengths(high):
    runs, cur = [], 0
    for b in high:
        if b:
            cur += 1
        else:
            if cur:
                runs.append(cur)
            cur = 0
    if cur:
        runs.append(cur)
    return runs


def grade(r, c1_flag, c2_flag, R_base, T_base):
    """风险分级。"""
    if R_base is not None and R_base < 0.01:
        return "无法判断"
    if T_base is None and not c1_flag:
        return "无法判断"
    if r >= 3:
        return "一级"
    if r >= 1.5:
        return "二级"
    if r > 0:
        return "三级"
    return "正常"


def infer(model, device_name, d):
    dev = model["设备"][device_name]
    th = model["阈值"]
    axes = dev["轴"]
    d["bin"] = d["Timestamp"].dt.floor("15min")

    cur = {}
    for col in axes:
        if col not in d.columns:
            continue
        v = d[col].abs().values
        R = float((v >= th["ACTIVE_TH"]).mean())
        act = v[v >= th["ACTIVE_TH"]]
        T = float(np.mean(act)) if len(act) >= th["MIN_ACTIVE_SAMPLES"] else None
        g = d.groupby("bin")[col].agg(n="count", hr=lambda x: float((x.abs() >= th["MID_HI"]).mean())).reset_index()
        g = g[g["n"] >= 5]
        runs = run_lengths((g["hr"] > th["HIGH_RATIO_TH"]).values)
        cur[col] = {"R": R, "T": T, "max_run": max(runs) if runs else 0}

    by_st = {}
    for col in cur:
        by_st.setdefault(axes[col]["工位"], []).append(col)
    st_dR = {}; st_dT = {}
    for st, cs in by_st.items():
        dR = [cur[c]["R"] * 100 - axes[c]["R_base"] * 100 for c in cs]
        dT = [((cur[c]["T"] - axes[c]["T_base"]) / axes[c]["T_base"] * 100)
              for c in cs if cur[c]["T"] is not None and axes[c].get("T_base")]
        st_dR[st] = float(np.median(dR)) if dR else 0.0
        st_dT[st] = float(np.median(dT)) if dT else 0.0

    rows = []
    for col in axes:
        if col not in d.columns:
            rows.append({"轴": col, "预警等级": "无法判断", "风险分": np.nan})
            continue
        b = axes[col]; R = cur[col]["R"]; T = cur[col]["T"]; max_run = cur[col]["max_run"]
        dR = (R - b["R_base"]) * 100
        dT = (T - b["T_base"]) / b["T_base"] * 100 if (T is not None and b.get("T_base")) else np.nan
        I_R = dR - st_dR[b["工位"]]
        I_T = dT - st_dT[b["工位"]] if np.isfinite(dT) else np.nan

        p99 = b.get("C1_run_P99", 1.0)
        ratio = max_run / p99 if p99 > 0 else 0.0
        c1_flag = max_run > max(2.0 * p99, 3)
        c2_flag = (abs(I_R) > th["C2独立活动率阈值pp"]) or (np.isfinite(I_T) and abs(I_T) > th["C2独立有效扭矩阈值%"])

        c1_score = min(ratio, 5.0) if c1_flag else 0.0
        c2_mag = max(abs(I_R) / th["C2独立活动率阈值pp"],
                     abs(I_T) / th["C2独立有效扭矩阈值%"] if np.isfinite(I_T) else 0.0)
        c2_score = min(c2_mag, 5.0) if c2_flag else 0.0
        r = max(c1_score, c2_score) + 0.5 * min(c1_score, c2_score)
        g = grade(r, c1_flag, c2_flag, b["R_base"], b.get("T_base"))

        if c1_flag and c2_flag:
            ev = "C1+C2双通道"
        elif c1_flag:
            ev = "C1突变"
        elif c2_flag:
            ev = "C2状态变化"
        else:
            ev = "无"
        rows.append({"轴": col, "机构": col.replace("最大扭矩", ""), "工位": b["工位"],
                     "预警等级": g, "风险分": round(r, 2),
                     "C1离群比": round(ratio, 2), "C1强度分": round(c1_score, 2),
                     "独立活动率pp": round(I_R, 2), "独立有效扭矩%": round(I_T, 1) if np.isfinite(I_T) else None,
                     "C2强度分": round(c2_score, 2), "证据类型": ev,
                     "维护建议": (
                         "优先核验纠偏机构机械阻力、张力、生产配方" if "纠偏" in col else
                         "核实张力、卷径、换卷及负载条件" if "放卷" in col else
                         "检查对应机构负载与运行参数" if ev != "无" else ""),
                     })
    return pd.DataFrame(rows)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--input", required=True)
    ap.add_argument("--output", required=True)
    ap.add_argument("--device", required=True)
    args = ap.parse_args()
    d = load_data(args.input)
    print(f"已读取数据：{len(d)} 行，时间范围 {d['Timestamp'].min()} ~ {d['Timestamp'].max()}")
    df = infer(load_model(args.model), args.device, d)

    # 只保留异常轴，按等级(严重在前)排序，同等级按风险分降序
    abnormal = df[df["预警等级"].isin(["一级", "二级", "三级"])].copy()
    order = {"一级": 0, "二级": 1, "三级": 2}
    abnormal["_等级序"] = abnormal["预警等级"].map(order)
    abnormal = abnormal.sort_values(["_等级序", "风险分"], ascending=[True, False]).reset_index(drop=True)
    abnormal["排名"] = range(1, len(abnormal) + 1)
    abnormal = abnormal.drop(columns=["_等级序"])

    cols = ["排名", "轴", "机构", "工位", "预警等级", "风险分", "证据类型",
            "C1离群比", "C1强度分", "独立活动率pp", "独立有效扭矩%", "C2强度分", "维护建议"]
    abnormal[cols].to_csv(args.output, index=False, encoding="utf-8-sig")

    print(f"异常轴共 {len(abnormal)} 根（一级 {int((abnormal['预警等级']=='一级').sum())} / 二级 {int((abnormal['预警等级']=='二级').sum())} / 三级 {int((abnormal['预警等级']=='三级').sum())}）")
    print(abnormal[cols].to_string(index=False))
