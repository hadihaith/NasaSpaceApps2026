"""Independent reference fixtures. Run with Python + SciPy; no NASA data involved."""
import json
from pathlib import Path
import numpy as np
import scipy
from scipy import stats

rng = np.random.default_rng(2804)
cases = []
for name, x, y in [
    ('hand-regression', np.arange(1., 6.), np.array([2., 4., 5., 4., 5.])),
    ('annual-noisy', np.arange(1980., 2020.), 15 + np.arange(40) * .08 + rng.normal(0, .4, 40)),
    ('tied-values', np.arange(20.), np.array([1, 2, 2, 4, 3, 5, 5, 6, 4, 8, 7, 9, 9, 8, 11, 10, 12, 13, 12, 14.])),
    ('irregular-time', np.array([0., 1., 2., 5., 8., 9., 11., 16., 19., 22., 25., 30.]), rng.normal(size=12)),
    ('falling-outlier', np.arange(30.), -np.arange(30.) * .1 + rng.normal(0, .2, 30)),
]:
    if name == 'falling-outlier': y[12] += 20
    ols = stats.linregress(x, y)
    sen = stats.theilslopes(y, x, .95, method='joint')
    critical = stats.t.ppf(.975, len(x) - 2)
    cases.append(dict(name=name, points=[dict(t=float(t), v=float(v)) for t,v in zip(x,y)],
        ols=dict(slope=ols.slope, intercept=ols.intercept, se=ols.stderr, p=ols.pvalue,
                 ci=[ols.slope-critical*ols.stderr, ols.slope+critical*ols.stderr]),
        sen=dict(slope=sen.slope, intercept=sen.intercept, ci=[sen.low_slope,sen.high_slope])))

# MK's continuity-corrected p-value has a distinct convention from scipy.kendalltau.
# Use an independently enumerated sign statistic and tie variance plus SciPy's normal tail.
mk = []
for values in ([1,2,2,3,4], list(range(10)), [1,3,2,5,4,7,6,9], [3]*10):
    v = np.asarray(values)
    s = int(np.sign(v[None,:]-v[:,None])[np.triu_indices(len(v),1)].sum())
    _, counts = np.unique(v,return_counts=True)
    variance = (len(v)*(len(v)-1)*(2*len(v)+5)-np.sum(counts*(counts-1)*(2*counts+5)))/18
    z = (s-np.sign(s))/np.sqrt(variance) if variance > 0 else 0
    mk.append(dict(values=values,s=s,varS=float(variance),p=float(2*stats.norm.sf(abs(z)))))
output = dict(generator='SciPy '+scipy.__version__, cases=cases, mannKendall=mk)
target = Path(__file__).resolve().parents[1]/'src/lib/stats/reference.json'
target.write_text(json.dumps(output, indent=2)+'\n')
print(f'Wrote {target} using SciPy {scipy.__version__}')
