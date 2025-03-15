# RV - Asset Optimizer Tool for Unity

The **RV - Asset Optimizer Tool** is an advanced editor extension designed for optimizing Unity projects targeting mobile and performance-sensitive platforms. It provides comprehensive asset management and optimization for textures, 3D models, audio clips, materials, and sprites. It integrates seamlessly into the Unity Editor, allowing both automated and manual optimization workflows.

## Features

- **Texture Optimization:**
  - Adjust maximum texture size
  - Select texture compression formats (e.g., ASTC)
  - Enable or disable mipmaps generation

- **Model Optimization:**
  - Polygon reduction using Quadric Error Metrics for efficient mesh simplification

- **Audio Optimization:**
  - Audio compression format selection (e.g., AAC)
  - Adjustable audio quality and load type settings (streaming, compressed in memory)

- **Material Optimization:**
  - Shader replacement with mobile-friendly shaders (e.g., Mobile/Diffuse)

- **Sprite Atlas Generation:**
  - Automatic creation of sprite atlases to improve rendering performance

- **Backup & Revert System:**
  - Automatically create backups before optimization
  - Easily revert changes to restore original assets

- **Profile Management:**
  - Create, save, and load optimization profiles for different asset categories (Textures, Models, Audio)

- **Manual and Automatic Asset Selection:**
  - Filter assets by folder or labels
  - Manual asset selection for granular control

- **Audit Logging:**
  - Persistent logging of optimization actions with timestamps and asset-specific details

- **Comprehensive Reporting:**
  - Detailed summary and statistics post-optimization
  - Export reports in CSV, JSON, or HTML formats

- **Customizable UI:**
  - Dark, Light, and Default theme options

---

## Installation

### Unity Package Manager (Recommended)

1. Open the Unity Package Manager (`Window > Package Manager`)
2. Click the **+** button and select **"Add package from git URL..."**
3. Enter the following URL:
   ```
   https://github.com/RomanVitolo/RV-AssetOptimizerTool.git#upm
   ```

### Manual Installation

1. Download the repository from:
   ```
   https://github.com/RomanVitolo/RV-AssetOptimizerTool
   ```
2. Extract the downloaded ZIP.
3. Copy the contents into your Unity project's `Assets/` directory.

---

## How to Use

### Opening the Tool
Navigate to:
```
RV - Template Tool > Asset Optimizer Tool
```

### Tabs Explanation

#### 1. **Settings**
Configure optimization parameters, asset filters, backup preferences, manual selection options, and UI appearance. You can load and save optimization profiles for quick reusability.

#### 2. **Preview**
Review which assets will be affected by the current settings. It displays asset counts and specific optimization parameters.

#### 3. **Report**
View optimization results, including detailed logs and graphical statistics. Export comprehensive reports in CSV, JSON, or HTML.

#### 4. **Manual Selection**
Manually select specific assets you want to optimize. Enable "Use Manual Selection" in the settings to activate this feature.

#### 5. **Audit History**
Access persistent logs recording all optimizations performed, allowing detailed review and accountability.

#### 6. **Help**
Quick reference and detailed documentation within the Unity Editor.

---

## Optimization Workflow

- **Step 1:** Adjust settings for each asset category or load saved optimization profiles.
- **Step 2:** Use the Preview tab to verify your optimization plan.
- **Step 3:** Execute optimization from the Settings tab.
- **Step 4:** Review the results in the Report tab and export if needed.
- **Optional:** Use Audit History for comprehensive historical logs.

---

## Modifying & Extending the Project

- **Scripts:**
  - Modify optimization logic in `AssetOptimizer.cs`
  - Extend profile capabilities via scripts (`TextureOptimizationProfile.cs`, `ModelOptimizationProfile.cs`, `AudioOptimizationProfile.cs`)

- **UI Customization:**
  - Adjust themes and GUI layouts in `AssetOptimizer.cs`

---

## Continuous Integration (CI/CD)
The tool supports CI/CD integration:
- Execute optimization through the `RunOptimizationCI` static method.
- Designed to integrate smoothly into automated Unity build pipelines.

---

## Dependencies
- **Unity Version:** Compatible with Unity 2019 and above (SpriteAtlas generation requires Unity 2017.1+)
- No external dependencies required

---

## Contribution
Feel free to fork, modify, and submit pull requests.

---

## License
This project is released under the MIT License. See `LICENSE.md` for details.

