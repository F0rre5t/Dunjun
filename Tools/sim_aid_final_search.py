"""Narrow search for Easy ~60% with finalized combat + aid model."""
import random
from sim_aid_balance import AidConfig, mix_clear_rate, clamp

random.seed(7)
results = []
for pot in (2.4, 2.8, 3.2):
    for thr in (0.22, 0.28, 0.34):
        for cd in (2, 3):
            for save in (0.45, 0.60, 0.75):
                for spike in (0.55, 0.65):
                    cfg = AidConfig(
                        name=f"p{pot}_t{thr}_cd{cd}_sv{save}_s{spike}",
                        potion_mult_max=pot,
                        drop_mult_max=1.2,
                        aid_threshold=thr,
                        cooldown_rooms=cd,
                        spike_mult_min=spike,
                        emergency_save_max=save,
                        aid_strength=1.0,
                    )
                    rate, by = mix_clear_rate(cfg, n=7000, rooms=12)
                    results.append((abs(rate - 0.60), rate, by, cfg))

results.sort(key=lambda x: x[0])
print("Closest to 60%:")
for err, rate, by, cfg in results[:10]:
    print(
        f"clear={rate:.3f} pot={cfg.potion_mult_max} thr={cfg.aid_threshold} "
        f"cd={cfg.cooldown_rooms} save={cfg.emergency_save_max} spike={cfg.spike_mult_min} by={by}"
    )

best = results[0][3]
print("\nRecommended:")
print(best)

for label, strength, rooms, save_scale in (
    ("Easy", 1.0, 12, 1.0),
    ("Normal", 0.40, 15, 0.25),
    ("Hard", 0.0, 20, 0.0),
):
    cfg = AidConfig(
        name=label,
        potion_mult_max=best.potion_mult_max,
        drop_mult_max=best.drop_mult_max,
        aid_threshold=best.aid_threshold,
        cooldown_rooms=best.cooldown_rooms,
        spike_mult_min=best.spike_mult_min,
        emergency_save_max=best.emergency_save_max * save_scale,
        aid_strength=strength,
    )
    rate, by = mix_clear_rate(cfg, n=12000, rooms=rooms)
    print(f"{label}: {rate:.3f} rooms={rooms} {by}")

none = AidConfig(name="none", aid_strength=0.0, emergency_save_max=0.0)
rate0, by0 = mix_clear_rate(none, n=12000, rooms=12)
print(f"Easy no-aid: {rate0:.3f} {by0}")
