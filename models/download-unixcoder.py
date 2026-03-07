"""
Downloads microsoft/unixcoder-base from HuggingFace, exports to ONNX, and
quantizes to int8 (~125 MB instead of ~500 MB, negligible quality loss for
embedding similarity).

Prerequisites:
    pip install -r requirements.txt

Usage:
    python download-unixcoder.py

Output files (in unixcoder/):
    model.onnx       - Quantized int8 ONNX model
    vocab.json        - Tokenizer vocabulary
    merges.txt        - BPE merge rules
"""

import json
import sys
from pathlib import Path


def extract_vocab_and_merges(tokenizer_json_path, output_dir):
    """Extract vocab.json and merges.txt from HuggingFace tokenizer.json."""
    with open(tokenizer_json_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    vocab = data["model"]["vocab"]
    merges = data["model"]["merges"]

    vocab_path = output_dir / "vocab.json"
    with open(vocab_path, "w", encoding="utf-8") as f:
        json.dump(vocab, f, ensure_ascii=False)
    print(f"  Extracted {len(vocab)} entries to {vocab_path}")

    merges_path = output_dir / "merges.txt"
    with open(merges_path, "w", encoding="utf-8") as f:
        f.write("#version: 0.2\n")
        for merge in merges:
            if isinstance(merge, list):
                merge = " ".join(merge)
            f.write(merge + "\n")
    print(f"  Extracted {len(merges)} merges to {merges_path}")


def main():
    try:
        from optimum.onnxruntime import ORTModelForFeatureExtraction
        from transformers import AutoTokenizer
        from onnxruntime.quantization import quantize_dynamic, QuantType
    except ImportError:
        print("Missing dependencies. Install with:")
        print("  pip install -r requirements.txt")
        sys.exit(1)

    output_dir = Path(__file__).parent / "unixcoder"
    fp32_dir = Path(__file__).parent / "unixcoder_fp32"

    model_name = "microsoft/unixcoder-base"
    print(f"Downloading and exporting {model_name} to ONNX...")

    # Export to ONNX via optimum (handles all torch.onnx complexity)
    ort_model = ORTModelForFeatureExtraction.from_pretrained(model_name, export=True)
    ort_model.save_pretrained(str(fp32_dir))

    # Save tokenizer
    tokenizer = AutoTokenizer.from_pretrained(model_name)
    tokenizer.save_pretrained(str(fp32_dir))

    fp32_path = fp32_dir / "model.onnx"
    fp32_size = fp32_path.stat().st_size / (1024 * 1024)
    print(f"Float32 model: {fp32_size:.1f} MB")

    # Quantize to int8
    output_dir.mkdir(exist_ok=True)
    onnx_path = output_dir / "model.onnx"
    print("Quantizing to int8...")
    quantize_dynamic(
        model_input=str(fp32_path),
        model_output=str(onnx_path),
        weight_type=QuantType.QInt8,
    )

    int8_size = onnx_path.stat().st_size / (1024 * 1024)
    print(f"Int8 model: {int8_size:.1f} MB (was {fp32_size:.1f} MB)")

    # Extract vocab.json and merges.txt from tokenizer.json
    tokenizer_json = fp32_dir / "tokenizer.json"
    if tokenizer_json.exists():
        print("Extracting vocab.json and merges.txt from tokenizer.json...")
        extract_vocab_and_merges(tokenizer_json, output_dir)
    else:
        print("ERROR: tokenizer.json not found, cannot extract vocab/merges")
        sys.exit(1)

    # Clean up fp32 intermediate directory
    import shutil
    shutil.rmtree(fp32_dir)

    # Verify expected files exist
    expected = ["vocab.json", "merges.txt", "model.onnx"]
    for f in expected:
        path = output_dir / f
        if path.exists():
            print(f"  OK  {path}")
        else:
            print(f"  MISSING  {path}")

    print("\nDone! Use with OnnxEmbeddingProvider.Create(")
    print(f'    modelPath: @"{onnx_path}",')
    print(f'    vocabPath: @"{output_dir / "vocab.json"}",')
    print(f'    mergesPath: @"{output_dir / "merges.txt"}")')


if __name__ == "__main__":
    main()
