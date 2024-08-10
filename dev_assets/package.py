import sys
import py7zr
import zipfile
import os
import hashlib
from tqdm import tqdm

def calculate_md5(file_path):
    # Create an MD5 hash object
    md5_hash = hashlib.md5()

    # Open the file in binary mode
    with open(file_path, 'rb') as file:
        # Read the file in chunks
        for chunk in iter(lambda: file.read(4096), b""):
            # Update the hash object with the chunk
            md5_hash.update(chunk)
    
    # Return the hexadecimal representation of the hash
    return md5_hash.hexdigest()

def get_all_files(dst_dir):
    file_list = set()
    for root, dirs, filenames in os.walk(dst_dir):
        for filename in filenames:
            file_path = os.path.join(root, filename)
            file_list.add(file_path)
    return file_list

def package_full(files, dst_dir, root_dir, version):
    os.makedirs(dst_dir, exist_ok=True)
    dst_file = os.path.join(dst_dir, f"LumiTracker_v{version}.7z")

    with py7zr.SevenZipFile(dst_file, 'w') as archive:
        with tqdm(total=len(files), unit='file') as pbar:
            for file in files:
                # Add file to the archive
                archive.write(file, arcname=os.path.relpath(file, root_dir))
                
                # Update the progress bar
                pbar.update(1)

def package_separate(files, ignored_files, dst_dir, root_dir, package_name):
    os.makedirs(dst_dir, exist_ok=True)
    dst_file = os.path.join(dst_dir, f"Package-{package_name}.zip")
    
    # Initialize the progress bar
    with zipfile.ZipFile(dst_file, 'w', zipfile.ZIP_DEFLATED) as zipf:
        # Wrap the files list with tqdm
        with tqdm(total=len(files), unit='file') as pbar:
            for file in files:
                file_path = os.path.join(dst_dir, file)
                if (file not in ignored_files) and os.path.isfile(file_path):
                    zipf.write(file_path, arcname=os.path.relpath(file, root_dir))
                # Update the progress bar
                pbar.update(1)
    # Update package metas
    md5  = calculate_md5(dst_file)
    size = os.path.getsize(dst_file)
    package_file = os.path.join(dst_dir, f"Package-{package_name}-{md5}.zip")
    os.rename(dst_file, package_file)
    meta_file = os.path.join(dst_dir, f"Package-{package_name}-{md5}-{size}.txt")
    with open(meta_file, 'w') as file:
        pass  # No need to write anything

def main(publish_dir):
    root_dir = os.path.join(publish_dir, "LumiTracker")
    app_dir  = ""
    for item in os.listdir(root_dir):
        item_path = os.path.join(root_dir, item)
        if os.path.isdir(item_path) and item.startswith("LumiTrackerApp"):
            app_dir = item_path
            break
    if not app_dir:
        return
    version = app_dir.split("-")[-1]

    print("Packaging Full App...")
    full_files = get_all_files(root_dir)
    package_full(files=full_files, dst_dir=publish_dir, root_dir=root_dir, version=version)

    ignored_files = set()

    print("Packaging Package-Assets...")
    assets_dir    = os.path.join(app_dir, "assets")
    images_dir    = os.path.join(assets_dir, "images")
    assets_files  = get_all_files(images_dir)
    package_separate(files=assets_files, ignored_files=set(), dst_dir=publish_dir, root_dir=assets_dir, package_name="Assets")
    ignored_files = ignored_files | assets_files

    print("Packaging Package-Python...")
    python_dir    = os.path.join(app_dir, "python")
    python_files  = get_all_files(python_dir)
    package_separate(files=python_files, ignored_files=set(), dst_dir=publish_dir, root_dir=python_dir, package_name="Python")
    ignored_files = ignored_files | python_files

    print("Packaging Package-Patch...")
    patch_files = get_all_files(app_dir)
    package_separate(files=patch_files, ignored_files=ignored_files, dst_dir=publish_dir, root_dir=app_dir, package_name="Patch")

if __name__ == "__main__":
    publish_dir = sys.argv[1]
    main(publish_dir)
