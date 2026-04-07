import os
import json
import glob

def generate_summary(runs_dir="./agent/temp/runs"):
    run_dirs = sorted(glob.glob(os.path.join(runs_dir, "*")), key=os.path.getmtime)
    
    runs_data = []
    
    for run_dir in run_dirs:
        if not os.path.isdir(run_dir) or os.path.basename(run_dir) == "scripts":
            continue
            
        # Find the benchmark JSON in this directory
        json_files = glob.glob(os.path.join(run_dir, "benchmark_*.json"))
        if not json_files:
            continue
            
        with open(json_files[0], 'r', encoding='utf-8') as f:
            data = json.load(f)
            
        # Check for hypothesis
        hypothesis_path = os.path.join(run_dir, "hypothesis.txt")
        hypothesis = ""
        if os.path.exists(hypothesis_path):
            with open(hypothesis_path, 'r', encoding='utf-8') as f:
                hypothesis = f.read().strip()
            
        dir_name = os.path.basename(run_dir)
        # Extract timestamp and tag from dirname format: YYYYMMDD_HHMMSS_tag
        parts = dir_name.split("_", 2)
        if len(parts) >= 3:
            timestamp = f"{parts[0]}_{parts[1]}"
            tag = parts[2]
        else:
            timestamp = dir_name
            tag = "unknown"
            
        gen = data.get("general_metrics", {})
        
        runs_data.append({
            "dir": dir_name,
            "timestamp": timestamp,
            "tag": tag,
            "sep_margin": gen.get("separation_margin", 0),
            "top1_acc": gen.get("top1_accuracy", 0) * 100,
            "f1_score": gen.get("f1_score", 0) * 100,
            "edge_acc": gen.get("edge_case_accuracy", 0) * 100,
            "time_ms": gen.get("avg_extraction_time_ms", 0),
            "hypothesis": hypothesis
        })

    if not runs_data:
        print("No benchmark runs found.")
        return

    # For console output
    console_lines = []
    console_lines.append(f"\n{'='*100}")
    console_lines.append(f"{'BENCHMARK SUMMARY REPORT':^100}")
    console_lines.append(f"{'='*100}\n")
    
    header_console = f"| {'Timestamp':<15} | {'Tag':<20} | {'Sep. Margin':<12} | {'Top-1 Acc %':<12} | {'F1 Score %':<12} | {'Edge Acc %':<12} | {'Avg Time (ms)':<12} |"
    console_lines.append(header_console)
    console_lines.append("-" * len(header_console))
    
    # For Markdown output
    md_lines = []
    md_lines.append("# Benchmark Summary Report\n")
    md_lines.append("| Timestamp | Tag | Sep. Margin | Top-1 Acc % | F1 Score % | Edge Acc % | Avg Time (ms) |")
    md_lines.append("|---|---|---|---|---|---|---|")

    for run in runs_data:
        sep_margin = run['sep_margin']
        sep_str = f"+{sep_margin}" if sep_margin > 0 else str(sep_margin)
        
        # Console row
        row_console = f"| {run['timestamp']:<15} | {run['tag']:<20} | {sep_str:<12} | {run['top1_acc']:<12.2f} | {run['f1_score']:<12.2f} | {run['edge_acc']:<12.2f} | {run['time_ms']:<12.2f} |"
        console_lines.append(row_console)
        
        # Markdown row
        row_md = f"| {run['timestamp']} | {run['tag']} | {sep_str} | {run['top1_acc']:.2f} | {run['f1_score']:.2f} | {run['edge_acc']:.2f} | {run['time_ms']:.2f} |"
        md_lines.append(row_md)
        
    console_lines.append(f"\n{'='*100}")
    md_lines.append("\n## Analysis\n")
    
    # Identify the baseline
    baseline_runs = [r for r in runs_data if r['tag'] == 'baseline']
    if baseline_runs:
        baseline = baseline_runs[-1]
        
        best_run = max(runs_data, key=lambda x: (x['sep_margin'], x['edge_acc']))
        sep_diff = best_run['sep_margin'] - baseline['sep_margin']
        
        # Console summary
        console_lines.append(f"\nBaseline Run: {baseline['dir']}")
        console_lines.append(f"Best Run:     {best_run['dir']}")
        console_lines.append(f"Improvement:  {sep_diff:+} Separation Margin points.")
        
        # Markdown summary
        md_lines.append(f"- **Baseline Run:** `{baseline['dir']}`")
        md_lines.append(f"- **Best Run:** `{best_run['dir']}`")
        md_lines.append(f"- **Improvement:** `{sep_diff:+}` Separation Margin points.\n")

    # Add Research Log (Hypotheses) to Markdown
    md_lines.append("## Research Log\n")
    for run in runs_data:
        if run['tag'] != 'baseline':
            md_lines.append(f"### {run['tag']} ({run['timestamp']})")
            md_lines.append(f"- **Result:** Sep. Margin: {run['sep_margin']} | Edge Acc: {run['edge_acc']:.2f}%")
            if run.get('hypothesis'):
                md_lines.append(f"- **Hypothesis:** {run['hypothesis']}")
            else:
                md_lines.append("- **Hypothesis:** (No hypothesis recorded)")
            md_lines.append("")

    full_console_output = "\n".join(console_lines)
    print(full_console_output)

    # Save to a markdown file in the runs directory
    md_path = os.path.join(runs_dir, "SUMMARY.md")
    with open(md_path, 'w', encoding='utf-8') as f:
        f.write("\n".join(md_lines))
        
    print(f"\nSummary successfully written to: {md_path}")

if __name__ == "__main__":
    generate_summary()
