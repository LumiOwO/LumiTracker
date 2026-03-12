import subprocess
import json
import os

def main():
    print("Testing Agent Auto-Loop Pipeline...")
    
    # Run the benchmark via WSL/Windows interop
    # We call python.exe directly because the Windows python environment has scikit-learn
    # We run it as a module from the src root
    project_root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    os.chdir(project_root)
    # Removing --samples argument so it tests comprehensively over all cards
    result = subprocess.run(["python.exe", "-m", "watcher.benchmark"], capture_output=True, text=True)
    
    if result.returncode != 0:
        print("Benchmark script failed to execute:")
        print(result.stderr)
        return
        
    print("Benchmark executed successfully.")
    
    # Check if JSON was produced
    json_path = os.path.join(project_root, "agent", "temp", "benchmark_baseline.json")
    if not os.path.exists(json_path):
        # Try without tag if baseline not found
        json_path = os.path.join(project_root, "agent", "temp", "benchmark_default.json")
        
    if not os.path.exists(json_path):
        print(f"Error: No benchmark result JSON found in {os.path.join(project_root, 'agent', 'temp')}")
        return
        
    try:
        with open(json_path, "r") as f:
            data = json.load(f)
            general = data.get("general_metrics", {})
            print("Successfully parsed benchmark JSON results.")
            print(f"F1 Score (General): {general.get('f1_score', 0):.4f}")
            print(f"Margin (General): {general.get('separation_margin', 0)}")
            print(f"Edge Case Accuracy: {general.get('edge_case_accuracy', 0):.4f}")
            print(f"Edge Case Avg Dist: {general.get('edge_case_avg_dist', 0):.2f}")
            print(f"Time (ms): {general.get('avg_extraction_time_ms', 0):.2f}")
    except json.JSONDecodeError:
        print("Failed to decode JSON.")
        
if __name__ == "__main__":
    main()
