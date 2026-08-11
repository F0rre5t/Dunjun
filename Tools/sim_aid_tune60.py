import random
from sim_aid_balance import AidConfig, mix_clear_rate

random.seed(11)
best = None
rows = []
for save in (0.55, 0.60, 0.65, 0.70):
    for pot in (2.6, 2.8, 3.0):
        for thr in (0.24, 0.28):
            cfg = AidConfig(
                name="f",
                potion_mult_max=pot,
                drop_mult_max=1.2,
                aid_threshold=thr,
                cooldown_rooms=2,
                spike_mult_min=0.60,
                emergency_save_max=save,
                aid_strength=1.0,
            )
            rate, by = mix_clear_rate(cfg, n=10000, rooms=12)
            err = abs(rate - 0.60)
            rows.append((err, rate, by, cfg))
            print(
                f"pot={pot} save={save} thr={thr} clear={rate:.3f} "
                f"avg={by['avg']:.2f} struggle={by['struggle']:.2f}"
            )

rows.sort(key=lambda x: x[0])
err, rate, by, cfg = rows[0]
print("\nBEST", rate, by)
print(
    "params",
    cfg.potion_mult_max,
    cfg.aid_threshold,
    cfg.cooldown_rooms,
    cfg.spike_mult_min,
    cfg.emergency_save_max,
    cfg.drop_mult_max,
)
print("noaid", mix_clear_rate(AidConfig(name="n", aid_strength=0.0, emergency_save_max=0.0), n=10000, rooms=12)[0])
norm = AidConfig(
    name="norm",
    potion_mult_max=cfg.potion_mult_max,
    drop_mult_max=cfg.drop_mult_max,
    aid_threshold=cfg.aid_threshold,
    cooldown_rooms=cfg.cooldown_rooms,
    spike_mult_min=cfg.spike_mult_min,
    emergency_save_max=cfg.emergency_save_max * 0.25,
    aid_strength=0.40,
)
print("Normal", mix_clear_rate(norm, n=10000, rooms=15)[0])
print("Hard", mix_clear_rate(AidConfig(name="h", aid_strength=0.0, emergency_save_max=0.0), n=10000, rooms=20)[0])
