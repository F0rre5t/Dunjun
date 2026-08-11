"""
Monte Carlo for dynamic aid design targets.
Easy ~12 combat-ish rooms, maxHP=5, boss at end.
Calibrate Easy clear rate toward ~60%.
"""
from __future__ import annotations

import random
from dataclasses import dataclass, field
from typing import List, Tuple


# --- Baseline combat model (abstracted from current tables) ---
# Expected damage taken per combat room by step band, skill-mix players.
# Mix: 35% skilled, 45% average, 20% struggling (for Easy target audience).

# Target: no-aid Easy mix ~40-45%; with aid ~60%.
# Units: expected HP lost per combat room (maxHP=5, ~10 combat rooms on Easy).
STEP_BASE_DMG = {
    # step: (mu_skilled, mu_avg, mu_struggle)
    0: (0.08, 0.17, 0.40),
    1: (0.10, 0.21, 0.48),
    2: (0.12, 0.28, 0.60),
    3: (0.14, 0.33, 0.70),
    4: (0.18, 0.39, 0.82),
    5: (0.20, 0.47, 0.95),
    6: (0.24, 0.55, 1.05),
    7: (0.26, 0.58, 1.12),
    8: (0.30, 0.66, 1.22),
    9: (0.32, 0.70, 1.30),
    10: (0.34, 0.75, 1.35),
    11: (0.36, 0.80, 1.40),
}

BOSS_DMG = {
    "skilled": (0.8, 1.8),
    "avg": (1.4, 2.85),
    "struggle": (2.5, 4.5),
}

# Loot: dropChance ~0.3, potion share among drops ~0.47 (from prefab weights)
BASE_DROP = 0.30
BASE_POTION_GIVEN_DROP = 0.47
SPIKE_BASE = {
    (0, 1): 0.0,
    (2, 3): 0.30,
    (4, 5): 0.50,
    (6, 7): 0.65,
    (8, 99): 0.80,
}
SPIKE_HIT_CHANCE_IF_PRESENT = 0.12  # chance spikes cost 1 hp this room


@dataclass
class AidConfig:
    name: str
    # distress weights
    w_hp: float = 0.40
    w_room_hurt: float = 0.30
    w_no_heal: float = 0.20
    w_near_death: float = 0.10
    # expected room damage by step for "hurt anomaly": approx avg curve
    # potion multiplier at distress 1.0
    potion_mult_max: float = 2.2
    drop_mult_max: float = 1.25  # only when distress high; still about potions path
    spike_mult_min: float = 0.55  # at distress 1.0
    # aid activation
    aid_threshold: float = 0.35
    cooldown_rooms: int = 2
    # near-death
    near_death_hp: int = 1
    # difficulty scalar on aid strength
    aid_strength: float = 1.0  # Easy 1.0, Normal 0.35, Hard 0.0
    # lethal damage: chance to survive at 1 HP when aid is eligible (Easy-focused)
    emergency_save_max: float = 0.55


@dataclass
class RunState:
    hp: int = 5
    max_hp: int = 5
    no_heal_streak: int = 0
    near_death_count: int = 0
    cooldown: int = 0
    rooms_cleared: int = 0
    potions_used: int = 0
    aid_triggers: int = 0


def clamp(x: float, a: float = 0.0, b: float = 1.0) -> float:
    return max(a, min(b, x))


def expected_dmg(step: int) -> float:
    # design expectation used inside formula (avg player curve)
    table = {
        0: 0.15, 1: 0.20, 2: 0.30, 3: 0.35, 4: 0.40, 5: 0.50,
        6: 0.55, 7: 0.60, 8: 0.70, 9: 0.75, 10: 0.80, 11: 0.85,
    }
    return table.get(step, 0.5 + (step - 5) * 0.05)


def spike_chance(step: int) -> float:
    for (lo, hi), p in SPIKE_BASE.items():
        if lo <= step <= hi:
            return p
    return 0.5


def sample_room_damage(step: int, skill: str) -> int:
    idx = {"skilled": 0, "avg": 1, "struggle": 2}[skill]
    base = STEP_BASE_DMG.get(step, STEP_BASE_DMG[11])[idx]
    # noise
    dmg = random.gauss(base, 0.35)
    # spike extra
    if random.random() < spike_chance(step) * SPIKE_HIT_CHANCE_IF_PRESENT:
        dmg += 1.0
    return max(0, int(round(dmg)))


def compute_distress(state: RunState, room_damage: int, step: int, cfg: AidConfig) -> float:
    hp_factor = 1.0 - (state.hp / state.max_hp)

    exp = max(0.35, expected_dmg(step))
    hurt_anom = clamp((room_damage / exp) - 0.6)  # only count excess struggle
    # map: 0 at expected*0.6, 1 at expected*2.0
    hurt_factor = clamp((room_damage / exp - 0.6) / 1.4)

    no_heal_factor = clamp(state.no_heal_streak / 4.0)
    near_death_factor = clamp(state.near_death_count / 3.0)

    d = (
        cfg.w_hp * hp_factor
        + cfg.w_room_hurt * hurt_factor
        + cfg.w_no_heal * no_heal_factor
        + cfg.w_near_death * near_death_factor
    )
    return clamp(d)


def aid_t(distress: float, cfg: AidConfig, state: RunState) -> float:
    if cfg.aid_strength <= 0.0 or distress < cfg.aid_threshold or state.cooldown > 0:
        return 0.0
    t = clamp((distress - cfg.aid_threshold) / max(1e-6, 1.0 - cfg.aid_threshold))
    return t * cfg.aid_strength


def aid_multipliers(distress: float, cfg: AidConfig, state: RunState) -> Tuple[float, float, float, bool]:
    """returns potion_w_mult, drop_mult, spike_mult, triggered"""
    t = aid_t(distress, cfg, state)
    if t <= 0.0:
        return 1.0, 1.0, 1.0, False

    potion_mult = 1.0 + (cfg.potion_mult_max - 1.0) * t
    drop_mult = 1.0 + (cfg.drop_mult_max - 1.0) * t
    spike_mult = 1.0 - (1.0 - cfg.spike_mult_min) * t
    return potion_mult, drop_mult, spike_mult, True


def try_potion_drop(enemies: int, potion_mult: float, drop_mult: float) -> bool:
    # each enemy rolls drop; if drop, potion with boosted weight
    # effective: P(potion from one enemy) = drop * (potion_share')
    # potion_share' = (p*m) / ((1-p) + p*m) where p is base potion share of table
    p = BASE_POTION_GIVEN_DROP
    m = potion_mult
    potion_share = (p * m) / ((1.0 - p) + p * m)
    drop_p = min(0.85, BASE_DROP * drop_mult)
    got = False
    for _ in range(enemies):
        if random.random() < drop_p and random.random() < potion_share:
            got = True
    return got


def enemies_for_step(step: int) -> int:
    if step <= 1:
        return 1
    if step <= 3:
        return random.choice([1, 2])
    if step <= 5:
        return random.choice([1, 2])
    if step <= 7:
        return random.choice([1, 2, 2])
    return random.choice([2, 2, 3])


def simulate_run(cfg: AidConfig, skill: str, rooms: int = 12) -> bool:
    """True if victory (survive all rooms + boss)."""
    st = RunState()
    # combat rooms: rooms-1 normal + 1 boss. Shop ignored for simplicity (~1 shop).
    combat_rooms = max(1, rooms - 2)  # subtract start fluff/shop approx
    # Use linear steps 0..combat_rooms-1 then boss
    for step in range(combat_rooms):
        if st.cooldown > 0:
            st.cooldown -= 1

        # apply spike pressure reduction by peeking distress from current hp only (pre-room)
        pre_distress = compute_distress(st, room_damage=0, step=step, cfg=cfg)
        _, _, spike_mult, _ = aid_multipliers(pre_distress, cfg, st)

        dmg = sample_room_damage(step, skill)
        # reduce spike portion roughly when spike_mult < 1
        if spike_mult < 1.0 and dmg > 0 and random.random() < (1.0 - spike_mult) * 0.5:
            dmg = max(0, dmg - 1)

        # distress from projected post-hit state for emergency + loot
        projected_hp = st.hp - dmg
        temp = RunState(
            hp=max(projected_hp, 0),
            max_hp=st.max_hp,
            no_heal_streak=st.no_heal_streak,
            near_death_count=st.near_death_count + (1 if 0 < projected_hp <= cfg.near_death_hp else 0),
            cooldown=st.cooldown,
        )
        distress = compute_distress(temp, dmg, step, cfg)
        pot_m, drop_m, _, _ = aid_multipliers(distress, cfg, st)

        st.hp -= dmg
        if st.hp <= cfg.near_death_hp and st.hp > 0:
            st.near_death_count += 1

        if st.hp <= 0:
            t = aid_t(distress, cfg, st)
            save_p = cfg.emergency_save_max * t
            if t > 0 and random.random() < save_p:
                st.hp = 1
                st.near_death_count += 1
                st.aid_triggers += 1
                st.cooldown = cfg.cooldown_rooms
            else:
                return False

        n_enemies = enemies_for_step(step)
        got_potion = try_potion_drop(n_enemies, pot_m, drop_m)
        if got_potion:
            heal = 2 if random.random() < 0.25 else 1
            st.hp = min(st.max_hp, st.hp + heal)
            st.no_heal_streak = 0
            st.potions_used += 1
            if aid_t(distress, cfg, st) > 0 and st.cooldown == 0:
                st.aid_triggers += 1
                st.cooldown = cfg.cooldown_rooms
        else:
            st.no_heal_streak += 1

        st.rooms_cleared += 1

    # Boss
    lo, hi = BOSS_DMG[skill]
    boss_dmg = int(round(random.uniform(lo, hi)))
    distress = compute_distress(st, max(1, boss_dmg // 2), step=combat_rooms, cfg=cfg)
    pot_m, drop_m, _, _ = aid_multipliers(distress, cfg, st)
    if try_potion_drop(2, pot_m, drop_m):
        st.hp = min(st.max_hp, st.hp + 1)
        st.no_heal_streak = 0
    st.hp -= boss_dmg
    if st.hp <= 0:
        t = aid_t(distress, cfg, st)
        if t > 0 and random.random() < cfg.emergency_save_max * t * 0.6:
            st.hp = 1
            return True
        return False
    return True


def mix_clear_rate(cfg: AidConfig, n: int = 8000, rooms: int = 12) -> Tuple[float, dict]:
    # Easy-picker audience: fewer highly skilled, more average
    skills = (["skilled"] * 30 + ["avg"] * 55 + ["struggle"] * 15)
    wins = 0
    by = {"skilled": [0, 0], "avg": [0, 0], "struggle": [0, 0]}
    for _ in range(n):
        skill = random.choice(skills)
        ok = simulate_run(cfg, skill, rooms=rooms)
        by[skill][1] += 1
        if ok:
            wins += 1
            by[skill][0] += 1
    rates = {k: (v[0] / v[1] if v[1] else 0) for k, v in by.items()}
    return wins / n, rates


def main():
    random.seed(42)
    candidates: List[AidConfig] = []

    # search a small grid for Easy ~0.60
    for pot_max in (2.2, 2.8, 3.4):
        for thr in (0.25, 0.30, 0.35):
            for cd in (2, 3):
                for save in (0.40, 0.55, 0.70):
                    for spike_min in (0.55, 0.70):
                        candidates.append(
                            AidConfig(
                                name=f"p{pot_max}_t{thr}_cd{cd}_sv{save}_s{spike_min}",
                                potion_mult_max=pot_max,
                                drop_mult_max=1.25,
                                aid_threshold=thr,
                                cooldown_rooms=cd,
                                aid_strength=1.0,
                                spike_mult_min=spike_min,
                                emergency_save_max=save,
                            )
                        )

    results = []
    for cfg in candidates:
        rate, by = mix_clear_rate(cfg, n=5000, rooms=12)
        results.append((abs(rate - 0.60), rate, by, cfg))

    results.sort(key=lambda x: x[0])
    print("=== Top Easy configs closest to 60% ===")
    for err, rate, by, cfg in results[:8]:
        print(
            f"{cfg.name}: clear={rate:.3f} "
            f"(skilled={by['skilled']:.2f} avg={by['avg']:.2f} struggle={by['struggle']:.2f}) "
            f"pot_max={cfg.potion_mult_max} thr={cfg.aid_threshold} cd={cfg.cooldown_rooms} spike_min={cfg.spike_mult_min}"
        )

    best = results[0][3]
    print("\n=== Baseline without aid (strength=0) ===")
    none = AidConfig(name="none", aid_strength=0.0)
    rate0, by0 = mix_clear_rate(none, n=5000, rooms=12)
    print(f"no-aid: clear={rate0:.3f} skilled={by0['skilled']:.2f} avg={by0['avg']:.2f} struggle={by0['struggle']:.2f}")

    print("\n=== Normal / Hard with best Easy shape ===")
    normal = AidConfig(
        name="normal",
        potion_mult_max=best.potion_mult_max,
        aid_threshold=best.aid_threshold,
        cooldown_rooms=best.cooldown_rooms,
        spike_mult_min=best.spike_mult_min,
        aid_strength=0.35,
    )
    hard = AidConfig(
        name="hard",
        potion_mult_max=best.potion_mult_max,
        aid_threshold=best.aid_threshold,
        cooldown_rooms=best.cooldown_rooms,
        spike_mult_min=best.spike_mult_min,
        aid_strength=0.0,
    )
    # longer runs
    for cfg, rooms in ((best, 12), (normal, 15), (hard, 20)):
        # adjust skill mix: normal more skilled, hard more skilled
        rate, by = mix_clear_rate(cfg, n=5000, rooms=rooms)
        print(f"{cfg.name} rooms={rooms} strength={cfg.aid_strength}: clear={rate:.3f} by={by}")

    # refine around best
    print("\n=== Local refine around best ===")
    refine = []
    for pot_max in (best.potion_mult_max - 0.2, best.potion_mult_max, best.potion_mult_max + 0.2):
        for thr in (best.aid_threshold - 0.05, best.aid_threshold, best.aid_threshold + 0.05):
            for save in (best.emergency_save_max - 0.1, best.emergency_save_max, best.emergency_save_max + 0.1):
                cfg = AidConfig(
                    name=f"refine_p{pot_max}_t{thr}_sv{save}",
                    potion_mult_max=round(pot_max, 2),
                    aid_threshold=round(max(0.15, thr), 2),
                    cooldown_rooms=best.cooldown_rooms,
                    spike_mult_min=best.spike_mult_min,
                    aid_strength=1.0,
                    drop_mult_max=best.drop_mult_max,
                    emergency_save_max=round(clamp(save), 2),
                )
                rate, by = mix_clear_rate(cfg, n=6000, rooms=12)
                refine.append((abs(rate - 0.60), rate, by, cfg))
    refine.sort(key=lambda x: x[0])
    for err, rate, by, cfg in refine[:6]:
        print(
            f"{cfg.name}: clear={rate:.3f} pot={cfg.potion_mult_max} thr={cfg.aid_threshold} "
            f"cd={cfg.cooldown_rooms} save={cfg.emergency_save_max} spike={cfg.spike_mult_min} by={by}"
        )

    final = refine[0][3]
    print("\n=== FINAL PICK ===")
    print(
        f"Easy: pot_max={final.potion_mult_max}, thr={final.aid_threshold}, cd={final.cooldown_rooms}, "
        f"drop_max={final.drop_mult_max}, spike_min={final.spike_mult_min}, emergency_save_max={final.emergency_save_max}"
    )
    for label, strength, rooms, save_scale in (
        ("Easy", 1.0, 12, 1.0),
        ("Normal", 0.35, 15, 0.35),
        ("Hard", 0.0, 20, 0.0),
    ):
        cfg = AidConfig(
            name=label,
            potion_mult_max=final.potion_mult_max,
            aid_threshold=final.aid_threshold,
            cooldown_rooms=final.cooldown_rooms,
            drop_mult_max=final.drop_mult_max,
            spike_mult_min=final.spike_mult_min,
            aid_strength=strength,
            emergency_save_max=final.emergency_save_max * save_scale,
        )
        rate, by = mix_clear_rate(cfg, n=10000, rooms=rooms)
        print(f"{label}: clear={rate:.3f} rooms={rooms} by={by}")
    none = AidConfig(name="none", aid_strength=0.0, emergency_save_max=0.0)
    rate0, by0 = mix_clear_rate(none, n=10000, rooms=12)
    print(f"Easy no-aid baseline: clear={rate0:.3f} by={by0}")


if __name__ == "__main__":
    main()
