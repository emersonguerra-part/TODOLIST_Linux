import os
import sys
import tarfile
import io
import time

def build_deb(publish_dir, icon_path, output_deb):
    # 1. debian-binary
    debian_binary = b"2.0\n"

    # 2. control.tar.gz
    control_content = """Package: todolist
Version: 1.0.0
Architecture: amd64
Maintainer: Emerson
Installed-Size: 150000
Section: utils
Priority: optional
Description: To-Do List Nativo com Sincronizacao em Nuvem
 Aplicativo de produtividade moderno construido com Avalonia UI (.NET 8).
 Suporte a sincronizacao Firebase Firestore, temas claro/escuro e efeitos sonoros.
"""
    control_bytes = control_content.encode("utf-8")
    
    control_tar_io = io.BytesIO()
    with tarfile.open(fileobj=control_tar_io, mode="w:gz") as tar:
        # ./control
        ti = tarfile.TarInfo(name="./control")
        ti.size = len(control_bytes)
        ti.mtime = int(time.time())
        ti.mode = 0o644
        ti.type = tarfile.REGTYPE
        tar.addfile(ti, io.BytesIO(control_bytes))
    control_tar_gz = control_tar_io.getvalue()

    # 3. data.tar.gz
    desktop_content = """[Desktop Entry]
Version=1.0
Type=Application
Name=To-Do List
GenericName=Gerenciador de Tarefas
Comment=Aplicativo de Gerenciamento de Tarefas com Sincronização em Nuvem
Exec=/opt/todolist/TodoListApp.Linux
Icon=com.emerson.todolist
Terminal=false
Categories=Utility;Office;ProjectManagement;
StartupWMClass=TodoListApp.Linux
"""
    desktop_bytes = desktop_content.encode("utf-8")
    
    with open(icon_path, "rb") as f:
        icon_bytes = f.read()

    data_tar_io = io.BytesIO()
    with tarfile.open(fileobj=data_tar_io, mode="w:gz") as tar:
        def add_dir(name):
            ti = tarfile.TarInfo(name=name)
            ti.type = tarfile.DIRTYPE
            ti.mode = 0o755
            ti.mtime = int(time.time())
            tar.addfile(ti)

        add_dir("./opt")
        add_dir("./opt/todolist")
        add_dir("./usr")
        add_dir("./usr/bin")
        add_dir("./usr/share")
        add_dir("./usr/share/applications")
        add_dir("./usr/share/icons")
        add_dir("./usr/share/icons/hicolor")
        add_dir("./usr/share/icons/hicolor/256x256")
        add_dir("./usr/share/icons/hicolor/256x256/apps")

        # Desktop file
        ti = tarfile.TarInfo(name="./usr/share/applications/com.emerson.todolist.desktop")
        ti.size = len(desktop_bytes)
        ti.mtime = int(time.time())
        ti.mode = 0o644
        ti.type = tarfile.REGTYPE
        tar.addfile(ti, io.BytesIO(desktop_bytes))

        # Icon file
        ti = tarfile.TarInfo(name="./usr/share/icons/hicolor/256x256/apps/com.emerson.todolist.png")
        ti.size = len(icon_bytes)
        ti.mtime = int(time.time())
        ti.mode = 0o644
        ti.type = tarfile.REGTYPE
        tar.addfile(ti, io.BytesIO(icon_bytes))

        # Symlink in /usr/bin/todolist
        ti = tarfile.TarInfo(name="./usr/bin/todolist")
        ti.type = tarfile.SYMTYPE
        ti.linkname = "/opt/todolist/TodoListApp.Linux"
        ti.mtime = int(time.time())
        ti.mode = 0o777
        tar.addfile(ti)

        # Files in /opt/todolist/
        for root, dirs, files in os.walk(publish_dir):
            rel_dir = os.path.relpath(root, publish_dir).replace("\\", "/")
            if rel_dir != ".":
                add_dir(f"./opt/todolist/{rel_dir}")
            for file in files:
                full_path = os.path.join(root, file)
                rel_file = os.path.join(rel_dir, file).replace("\\", "/") if rel_dir != "." else file
                with open(full_path, "rb") as f:
                    content = f.read()
                
                ti = tarfile.TarInfo(name=f"./opt/todolist/{rel_file}")
                ti.size = len(content)
                ti.mtime = int(time.time())
                # If executable or library/script
                if file.endswith(".Linux") or file == "TodoListApp.Linux":
                    ti.mode = 0o755
                else:
                    ti.mode = 0o644
                ti.type = tarfile.REGTYPE
                tar.addfile(ti, io.BytesIO(content))

    data_tar_gz = data_tar_io.getvalue()

    # 4. Pack into ar archive (.deb)
    def make_ar_header(name, size):
        name_str = (name + "/").ljust(16)
        mtime_str = str(int(time.time())).ljust(12)
        uid_str = "0".ljust(6)
        gid_str = "0".ljust(6)
        mode_str = "100644".ljust(8)
        size_str = str(size).ljust(10)
        trailer = "`\n"
        header = (name_str + mtime_str + uid_str + gid_str + mode_str + size_str + trailer).encode("ascii")
        assert len(header) == 60
        return header

    with open(output_deb, "wb") as deb:
        deb.write(b"!<arch>\n")

        # debian-binary
        deb.write(make_ar_header("debian-binary", len(debian_binary)))
        deb.write(debian_binary)
        if len(debian_binary) % 2 != 0:
            deb.write(b"\n")

        # control.tar.gz
        deb.write(make_ar_header("control.tar.gz", len(control_tar_gz)))
        deb.write(control_tar_gz)
        if len(control_tar_gz) % 2 != 0:
            deb.write(b"\n")

        # data.tar.gz
        deb.write(make_ar_header("data.tar.gz", len(data_tar_gz)))
        deb.write(data_tar_gz)
        if len(data_tar_gz) % 2 != 0:
            deb.write(b"\n")

    print(f"DEB package successfully created: {output_deb} ({os.path.getsize(output_deb)} bytes)")

if __name__ == "__main__":
    publish_dir = r"D:\Documetos\Emerson\TODOLIST_Linux\publish_linux"
    icon_path = r"D:\Documetos\Emerson\TODOLIST_Linux\app_icon.png"
    output_deb = r"D:\Documetos\Emerson\TODOLIST_Linux\todolist_1.0.0_amd64.deb"
    build_deb(publish_dir, icon_path, output_deb)
