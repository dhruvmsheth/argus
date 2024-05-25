import os
from dataclasses import dataclass
from pathlib import Path

import h5py
import kornia
import matplotlib.pyplot as plt
import mujoco
import torch
import tyro

from argus import ROOT
from argus.data import CameraCubePoseDatasetConfig
from argus.models import NCameraCNN, NCameraCNNConfig
from argus.utils import get_pose, xyzxyzw_to_xyzwxyz_SE3


@dataclass
class ValRealConfig:
    """Configuration for the validation test.

    Fields:
        real_data_path: The path to the real-world data.
    """

    model_path: str
    dataset_config: CameraCubePoseDatasetConfig


def validate_real(cfg: ValRealConfig) -> None:
    """Validate model performance on real-world data visually."""
    # load pose estimator model
    device = "cuda" if torch.cuda.is_available() else "cpu"
    model = NCameraCNN(NCameraCNNConfig(), W=cfg.dataset_config.W, H=cfg.dataset_config.H).to(device)
    model.load_state_dict(torch.load(cfg.model_path))
    model.eval()

    # mujoco model for rendering
    m = mujoco.MjModel.from_xml_path(ROOT + "/mujoco/leap/task.xml")
    d = mujoco.MjData(m)
    renderer = mujoco.Renderer(m, model.H, model.W)
    geom_name_to_hide = "goal"
    geom_id = d.body(geom_name_to_hide).id
    mujoco.mj_forward(m, d)

    with h5py.File(cfg.dataset_config.dataset_path, "r") as f:
        images = f["images"]  # (num_images, 2, H, W, 3)

        H, W = cfg.dataset_config.H, cfg.dataset_config.W
        for ii, img_pair in enumerate(images):
            long_img_np = img_pair.transpose(0, 3, 1, 2)  # (2, 3, H, W)
            float_img = torch.from_numpy(long_img_np).to(device).to(torch.float32) / 255.0
            if cfg.dataset_config.center_crop:
                float_img = kornia.geometry.transform.center_crop(float_img, (H, W))
                float_img = float_img.reshape(-1, H, W).unsqueeze(0)  # (1, 6, H, W)

            pred_pose_xyzw = get_pose(float_img, model)[0]  # (num_images, 7)
            pred_pose_wxyz = xyzxyzw_to_xyzwxyz_SE3(pred_pose_xyzw)  # (num_images, 7)
            pred_pose = pred_pose_wxyz.detach().cpu().numpy()
            d.qpos[:7] = pred_pose
            mujoco.mj_forward(m, d)

            float_img_numpy = float_img.detach().cpu().numpy()
            float_img_numpy = float_img_numpy.reshape(2, 3, H, W).transpose(0, 2, 3, 1)

            # top row: side 1
            plt.subplot(2, 2, 1)
            plt.imshow(float_img_numpy[0])

            plt.subplot(2, 2, 2)
            m.geom_rgba[geom_id, 3] = 0
            renderer.update_scene(d, camera="cam1")
            m.geom_rgba[geom_id, 3] = 1
            pred_img1 = renderer.render()
            plt.imshow(pred_img1)

            # bottom row: side 2
            plt.subplot(2, 2, 3)
            plt.imshow(float_img_numpy[1])

            plt.subplot(2, 2, 4)
            m.geom_rgba[geom_id, 3] = 0
            renderer.update_scene(d, camera="cam2")
            m.geom_rgba[geom_id, 3] = 1
            pred_img2 = renderer.render()
            plt.imshow(pred_img2)

            plt.axis("off")
            plt.suptitle(f"Pred pose {ii}: {pred_pose}")
            output_dir = Path(ROOT) / f"outputs/real_validation_visuals/{Path(cfg.model_path).stem}"
            os.makedirs(output_dir, exist_ok=True)
            # Save figure to file.
            plt.savefig(output_dir / f"example_{ii}.png")


if __name__ == "__main__":
    cfg = tyro.cli(ValRealConfig)
    validate_real(cfg)
