# VR / MR Duty-Free Prototype (PICO XR)

> Developed during the 48-hour **nextReality x PICO XR Hackathon 2026** as a participant / tech developer.

---

## 🛍️ About the Project

- **The Challenge:** 48 hours to build a Mixed-Reality prototype with zero prior experience in Unity, Blender, or the PICO SDK.
- **The Project:** A virtual Duty-Free area for **Gebr. Heinemann** featuring a real-time visitor heatmap.
- **The Tech Stack:** Unity 2022.3, C#, Blender 4.0, custom HLSL shaders, and Python.
- **Problem-Solving:** Handled cross-platform SDK conflicts (macOS vs. Windows) and complex Git merge issues. When broken FBX exports generated over 105 incorrect animation takes per file, created a custom Python script with a name-matching algorithm to automate the data cleanup.
- **The Result:** A fully functional demo deployed on the PICO 4 headset and an honorary poster from the HCI working group at the University of Hamburg.

---

## 🚀 Features & Components
- **XR Interaction Toolkit (XRI):** Interactive VR/MR elements and controls.
- **Slot Management (`SlotManager.cs`):** Game/interaction slot handling.
- **See-Through / Passthrough (`SeeThroughActivator.cs`):** Mixed Reality passthrough management.
- **Heatmap Visualization (`Heatmap/`, `Segment.cs`):** Real-time visitor tracking and heatmap data analytics.
- **Color Materials:** Predefined custom materials (Red, Blue, Green, Orange, Pink, Brown).

---

## ⚙️ Setup & Installation

Since the PICO Unity Integration SDK is proprietary software owned by *PICO Technology Co., Ltd.* and subject to strict redistribution restrictions, it cannot be hosted directly in this public repository. You will need to add the SDK locally after cloning.

### Setup Steps:

1. **Clone the repository:**
   ```bash
   git clone <YOUR_REPO_URL>
   cd <REPOSITORY_NAME>
   ```

2. **Download the PICO Unity Integration SDK:**
   * Download the required version of the **PICO Unity Integration SDK** (v3.4.0 or newer) from the official PICO Developer Platform.

3. **Link the SDK:**
   * Add the local package path in `Packages/manifest.json`:
     ```json
     "com.unity.xr.picoxr": "file:../PATH/TO/PICO Unity Integration SDK"
     ```
   * Alternatively, place the SDK folder inside `Packages/` or `Assets/`.

4. **Open in Unity:**
   * Open Unity Hub, click **Add project from disk**, and select this project folder.
   * Recommended Unity Version: Unity 2022.3 (check `ProjectSettings/ProjectVersion.txt`).

---

## 📁 Project Structure
```text
├── Assets/
│   ├── Scenes/           # Unity Scenes (e.g. SampleScene)
│   ├── Scripts/          # Custom scripts (SlotManager, SeeThroughActivator, Heatmap, etc.)
│   ├── Materials/        # Custom color materials
│   └── ...
├── Packages/
│   └── manifest.json     # Package manifest (including PICO XR reference)
├── ProjectSettings/      # Unity project settings (Input, Physics, XR, etc.)
└── .gitignore            # Unity Git ignore (excludes Library, Temp, etc.)
```

---

## 📄 License
This project is open-source under the repository license. The PICO Unity Integration SDK remains the property of *PICO Technology Co., Ltd.* and is subject to its respective license agreement.
