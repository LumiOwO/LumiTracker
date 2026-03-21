import os
import json
import glob

def generate_summary(runs_dir="./agent/temp/runs"):
    run_dirs = sorted(glob.glob(os.path.join(runs_dir, "*")), key=os.path.getmtime)
    
    runs_data = []
    
    for run_dir in run_dirs:
        if not os.path.isdir(run_dir):
            continue
            
        # Find the benchmark JSON in this directory
        json_files = glob.glob(os.path.join(run_dir, "benchmark_*.json"))
        if not json_files:
            continue
            
        with open(json_files[0], 'r', encoding='utf-8') as f:
            data = json.load(f)
            
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
            "time_ms": gen.get("avg_extraction_time_ms", 0)
        })

    if not runs_data:
        print("No benchmark runs found.")
        return

    output_lines = []
    output_lines.append(f"\n{'='*100}")
    output_lines.append(f"{'BENCHMARK SUMMARY REPORT':^100}")
    output_lines.append(f"{'='*100}\n")
    
    # Print Header
    header = f"| {'Timestamp':<15} | {'Tag':<20} | {'Sep. Margin':<12} | {'Top-1 Acc %':<12} | {'F1 Score %':<12} | {'Edge Acc %':<12} | {'Avg Time (ms)':<12} |"
    output_lines.append(header)
    output_lines.append("-" * len(header))
    
    for run in runs_data:
        sep_margin = run['sep_margin']
        # Highlight positive separation margin
        sep_str = f"+{sep_margin}" if sep_margin > 0 else str(sep_margin)
        
        row = f"| {run['timestamp']:<15} | {run['tag']:<20} | {sep_str:<12} | {run['top1_acc']:<12.2f} | {run['f1_score']:<12.2f} | {run['edge_acc']:<12.2f} | {run['time_ms']:<12.2f} |"
        output_lines.append(row)
        
    output_lines.append(f"\n{'='*100}")
    
    # Identify the baseline
    baseline_runs = [r for r in runs_data if r['tag'] == 'baseline']
    if baseline_runs:
        baseline = baseline_runs[-1] # latest baseline
        output_lines.append(f"\nBaseline Run: {baseline['dir']}")
        
        best_run = max(runs_data, key=lambda x: (x['sep_margin'], x['edge_acc']))
        output_lines.append(f"Best Run:     {best_run['dir']}")
        
        sep_diff = best_run['sep_margin'] - baseline['sep_margin']
        output_lines.append(f"Improvement:  {sep_diff:+} Separation Margin points.")

    full_output = "\n".join(output_lines)
    print(full_output)

    # Save to a markdown file in the runs directory
    md_path = os.path.join(runs_dir, "SUMMARY.md")
    with open(md_path, 'w', encoding='utf-8') as f:
        f.write("# Benchmark Summary Report\n\n```text\n")
        f.write(full_output)
        f.write("\n```\n")
    print(f"\nSummary successfully written to: {md_path}")

if __name__ == "__main__":
    generate_summary()
