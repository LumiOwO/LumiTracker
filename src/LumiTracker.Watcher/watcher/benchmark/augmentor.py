import cv2
import numpy as np
import random

class ImageAugmentor:
    @staticmethod
    def apply_random_brightness_contrast(image, alpha_range=(0.7, 1.3), beta_range=(-40, 40)):
        alpha = random.uniform(*alpha_range) # Contrast
        beta = random.uniform(*beta_range)   # Brightness
        adjusted = cv2.convertScaleAbs(image, alpha=alpha, beta=beta)
        return adjusted

    @staticmethod
    def apply_random_blur(image, kernel_sizes=[(5, 5), (7, 7)]):
        ksize = random.choice(kernel_sizes)
        return cv2.GaussianBlur(image, ksize, 0)
        
    @staticmethod
    def apply_random_noise(image, noise_type="gaussian"):
        if noise_type == "gaussian":
            row, col, ch = image.shape
            mean = 0
            var = 50
            sigma = var**0.5
            gauss = np.random.normal(mean, sigma, (row, col, ch))
            gauss = gauss.reshape(row, col, ch)
            noisy = image + gauss
            return np.clip(noisy, 0, 255).astype(np.uint8)
        return image

    @staticmethod
    def apply_scale(image, scale=0.5):
        h, w = image.shape[:2]
        new_w = int(w * scale)
        new_h = int(h * scale)
        scaled = cv2.resize(image, (new_w, new_h), interpolation=cv2.INTER_LINEAR)
        # Pad back to original size with black borders so the crop box still fits roughly
        # Actually, in runtime, the CropBox scales. But for Benchmark feeding, we just resize.
        # However, to avoid breaking the handler's fixed FrameBuffer expectations, we scale down, then scale UP.
        # This simulates the loss of high frequency information.
        restored = cv2.resize(scaled, (w, h), interpolation=cv2.INTER_LINEAR)
        return restored
        
    @staticmethod
    def apply_translation(image, max_pixels=5):
        h, w = image.shape[:2]
        tx = random.randint(-max_pixels, max_pixels)
        ty = random.randint(-max_pixels, max_pixels)
        translation_matrix = np.float32([[1, 0, tx], [0, 1, ty]])
        translated = cv2.warpAffine(image, translation_matrix, (w, h), borderMode=cv2.BORDER_REPLICATE)
        return translated

    @staticmethod
    def apply_glare(image):
        h, w = image.shape[:2]
        # Create a blank image
        glare = np.zeros_like(image, dtype=np.uint8)
        
        # Add a polygon simulating a glare beam
        pt1 = (random.randint(0, w//2), 0)
        pt2 = (pt1[0] + random.randint(20, 80), 0)
        pt3 = (random.randint(w//2, w), h)
        pt4 = (pt3[0] - random.randint(20, 80), h)
        
        pts = np.array([pt1, pt2, pt3, pt4], np.int32)
        pts = pts.reshape((-1, 1, 2))
        cv2.fillPoly(glare, [pts], (255, 255, 255, 255) if image.shape[2] == 4 else (255, 255, 255))
        
        # Blend
        alpha = 0.4
        blended = cv2.addWeighted(image, 1 - alpha, glare, alpha, 0)
        return blended

    @staticmethod
    def apply_holographic_noise(image):
        h, w = image.shape[:2]
        # Generate some frequency noise
        noise = np.random.randint(0, 255, (h, w, image.shape[2]), dtype=np.uint8)
        
        # Create a mask for where the noise should appear (simulating specific holographic parts)
        mask = np.zeros((h, w), dtype=np.uint8)
        cv2.circle(mask, (w//2, h//2), min(w, h)//3, 255, -1)
        
        # Blur the mask to make it smooth
        mask = cv2.GaussianBlur(mask, (51, 51), 0)
        mask = mask.astype(np.float32) / 255.0
        
        # Expand mask to multiple channels
        if image.shape[2] == 4:
            mask_ch = np.stack([mask, mask, mask, np.ones_like(mask)], axis=2)
        else:
            mask_ch = np.stack([mask, mask, mask], axis=2)
            
        alpha = 0.3
        # Apply noise only where mask is
        blended = image * (1 - mask_ch * alpha) + noise * (mask_ch * alpha)
        return blended.astype(np.uint8)
