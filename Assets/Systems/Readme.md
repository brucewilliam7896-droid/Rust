# Systems

Cross-cutting simulation services that run on the fixed tick (`SimulationClock`, 50 Hz): decay, spawning, day/night, scheduling.
Every expensive system declares a per-tick budget (architecture rule 6).
