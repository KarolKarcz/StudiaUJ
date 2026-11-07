"""
Problem plecakowy - algorytm (1+1) z mutacją jednego bitu.

Idea:
  * na starcie mamy dwie identyczne tablice bitowe: RODZIC i POTOMEK
    (bit = 1 -> przedmiot w plecaku, bit = 0 -> poza plecakiem),
  * potomek jest kopią rodzica, w której mutujemy JEDEN losowy bit,
  * porównujemy oba rozwiązania:
      - oba dopuszczalne            -> wygrywa większa wartość,
      - tylko jedno dopuszczalne    -> wygrywa dopuszczalne,
      - oba niedopuszczalne         -> wygrywa to bliżej dopuszczalności
                                       (mniejsze przekroczenie pojemności),
  * zwycięzca staje się rodzicem w kolejnej iteracji.

Wymagania: python 3.8+, matplotlib (tylko do wizualizacji).
"""

import random

import matplotlib.pyplot as plt

# ============================ KONFIGURACJA ============================
CONFIG = {
    "volumes": [12, 7, 11, 8, 9, 6, 14, 5, 10, 13],  # objętości przedmiotów
    "values": None,          # wartości przedmiotów; None = wartość równa objętości
    "capacity": 40,          # pojemność plecaka
    "iterations": 300,       # liczba iteracji algorytmu
    "seed": 42,              # ziarno losowości (None = za każdym razem inaczej)
    "show_plot": True,       # czy otworzyć okno z wykresem
    "save_plot_path": "knapsack_result.png",  # gdzie zapisać wykres (None = nie zapisuj)
}
# ======================================================================


def evaluate(bits, volumes, values, capacity):
    """Oblicza objętość, wartość i przekroczenie pojemności rozwiązania."""
    total_vol = sum(v for b, v in zip(bits, volumes) if b)
    total_val = sum(v for b, v in zip(bits, values) if b)
    feasible = total_vol <= capacity
    excess = max(0, total_vol - capacity)
    return {"vol": total_vol, "val": total_val, "feasible": feasible, "excess": excess}


def is_better(child, parent):
    """True, jeśli potomek ma zastąpić rodzica."""
    if child["feasible"] and parent["feasible"]:
        return child["val"] >= parent["val"]          # oba dopuszczalne -> lepsza wartość
    if child["feasible"] != parent["feasible"]:
        return child["feasible"]                      # dopuszczalne zawsze wygrywa
    return child["excess"] <= parent["excess"]        # oba niedopuszczalne -> bliżej dopuszczalności


def run(config):
    rng = random.Random(config["seed"])
    volumes = config["volumes"]
    values = config["values"] or volumes
    capacity = config["capacity"]
    n = len(volumes)

    # Start: dwie identyczne tablice bitowe (rodzic i potomek)
    parent = [rng.randint(0, 1) for _ in range(n)]
    child = parent.copy()
    parent_eval = evaluate(parent, volumes, values, capacity)

    history = [{"it": 0, **parent_eval, "accepted": True, "mutated": None}]

    for it in range(1, config["iterations"] + 1):
        child = parent.copy()                 # potomek = kopia rodzica
        idx = rng.randrange(n)               # mutacja jednego bitu
        child[idx] ^= 1
        child_eval = evaluate(child, volumes, values, capacity)

        accepted = is_better(child_eval, parent_eval)
        if accepted:
            parent, parent_eval = child.copy(), child_eval

        history.append({"it": it, **parent_eval, "accepted": accepted, "mutated": idx})

    return parent, parent_eval, history, values


def print_report(bits, result, volumes, values, capacity):
    print("=" * 56)
    print(f"Pojemność plecaka: {capacity}")
    print(f"{'Przedmiot':<10}{'Obj.':>6}{'Wart.':>7}{'Bit':>5}")
    for i, (v, val, b) in enumerate(zip(volumes, values, bits)):
        print(f"P{i + 1:<9}{v:>6}{val:>7}{b:>5}")
    print("-" * 56)
    print(f"Objętość rozwiązania : {result['vol']} / {capacity}")
    print(f"Wartość rozwiązania  : {result['val']}")
    status = "DOPUSZCZALNE" if result["feasible"] else f"NIEDOPUSZCZALNE (przekroczenie {result['excess']})"
    print(f"Status               : {status}")
    print("=" * 56)


def plot(history, bits, volumes, capacity, save_path, show):
    its = [h["it"] for h in history]
    vols = [h["vol"] for h in history]
    excess = [h["excess"] for h in history]
    feas = [h["feasible"] for h in history]
    n = len(volumes)

    bg, panel, grid, text = "#0f172a", "#1e293b", "#94a3b8", "#e2e8f0"
    fig = plt.figure(figsize=(13, 8), facecolor=bg)
    gs = fig.add_gridspec(2, 2, hspace=0.45, wspace=0.25)
    ax_vol = fig.add_subplot(gs[0, :])
    ax_exc = fig.add_subplot(gs[1, 0])
    ax_itm = fig.add_subplot(gs[1, 1])

    for ax in (ax_vol, ax_exc, ax_itm):
        ax.set_facecolor(panel)
        for s in ax.spines.values():
            s.set_color("#334155")
        ax.tick_params(colors=text)
        ax.title.set_color("#f8fafc")
        ax.xaxis.label.set_color(text)
        ax.yaxis.label.set_color(text)
        ax.grid(alpha=0.15, color=grid)

    # 1) objętość rodzica w czasie
    ax_vol.plot(its, vols, color="#38bdf8", lw=1.8, label="objętość rodzica")
    ax_vol.axhline(capacity, color="#f43f5e", ls="--", lw=1.5, label=f"pojemność = {capacity}")
    ok = [(i, v) for i, v, f in zip(its, vols, feas) if f]
    bad = [(i, v) for i, v, f in zip(its, vols, feas) if not f]
    ax_vol.scatter(*zip(*ok), s=12, color="#22c55e", zorder=3, label="dopuszczalne") if ok else None
    ax_vol.scatter(*zip(*bad), s=12, color="#f59e0b", zorder=3, label="niedopuszczalne") if bad else None
    ax_vol.set_title("Objętość rozwiązania rodzica w kolejnych iteracjach")
    ax_vol.set_xlabel("iteracja")
    ax_vol.set_ylabel("objętość")
    ax_vol.legend(facecolor=panel, labelcolor=text, edgecolor="#334155")

    # 2) odległość od dopuszczalności
    ax_exc.step(its, excess, where="post", color="#f59e0b", lw=1.6)
    ax_exc.fill_between(its, excess, step="post", color="#f59e0b", alpha=0.2)
    ax_exc.set_title("Przekroczenie pojemności (0 = dopuszczalne)")
    ax_exc.set_xlabel("iteracja")
    ax_exc.set_ylabel("przekroczenie")

    # 3) przedmioty: zielone = w plecaku
    colors = ["#22c55e" if b else "#475569" for b in bits]
    bars = ax_itm.bar(range(n), volumes, color=colors)
    for bar, v in zip(bars, volumes):
        ax_itm.text(bar.get_x() + bar.get_width() / 2, v + 0.3, str(v),
                    ha="center", color=text, fontsize=9)
    ax_itm.set_xticks(range(n))
    ax_itm.set_xticklabels([f"P{i + 1}" for i in range(n)], color=text)
    ax_itm.set_title("Przedmioty (zielone = w plecaku)")
    ax_itm.set_ylabel("objętość")

    chosen = sum(v for v, b in zip(volumes, bits) if b)
    fig.suptitle(f"Problem plecakowy - (1+1) z mutacją bitu  |  wynik: {chosen}/{capacity}",
                 color="#f8fafc", fontsize=15, fontweight="bold")

    if save_path:
        fig.savefig(save_path, dpi=150, facecolor=bg, bbox_inches="tight")
        print(f"Wykres zapisano: {save_path}")
    if show:
        plt.show()
    plt.close(fig)


def main():
    cfg = CONFIG
    bits, result, history, values = run(cfg)
    print_report(bits, result, cfg["volumes"], values, cfg["capacity"])
    plot(history, bits, cfg["volumes"], cfg["capacity"], cfg["save_plot_path"], cfg["show_plot"])


if __name__ == "__main__":
    main()
