import pandas as pd
import statsmodels.api as sm
from scipy.stats import chi2_contingency, ttest_ind
from sklearn.linear_model import LogisticRegression

df = pd.read_csv("./DanoHackAnalysis/data.csv")
df['age_group'] = df['client_age'].apply(lambda x: 'youth' if x <= 35 else 'adult')
df_filtered = df[df['client_agree_flg'].isin([1, -1])].copy()
df_filtered['agree_binary'] = (df_filtered['client_agree_flg'] == 1).astype(int)

# Проверка по возрастным группам
contingency_table = pd.crosstab(df_filtered['age_group'], df_filtered['client_agree_flg'] == 1)
print("Таблица согласия по возрастным группам:")
print(contingency_table)
chi2, p_chi2, _, _ = chi2_contingency(contingency_table)
print(f"\nХи-квадрат тест: Chi2 = {chi2:.4f}, p-value = {p_chi2:.4f}")

group_youth = df_filtered[df_filtered['age_group'] == 'youth']['agree_binary']
group_adult = df_filtered[df_filtered['age_group'] == 'adult']['agree_binary']
t_stat, p_ttest = ttest_ind(group_youth, group_adult, equal_var=False)
print(f"t-тест: t = {t_stat:.4f}, p-value = {p_ttest:.4f}")

if p_chi2 < 0.05 and p_ttest < 0.05:
    print("\nГипотеза по возрасту подтверждается: статистически значимые различия между возрастными группами по согласию на услуги банка.")
    
    # Проверка по полу клиента
    print("\nПроверка различий по полу клиента:")
    for gender in ['Мужчина', 'Женщина']:
        group = df_filtered[df_filtered['client_gender'] == gender]['agree_binary']
        print(f"Средняя доля согласия {gender}: {group.mean():.4f}, Размер выборки: {len(group)}")

    group_male = df_filtered[df_filtered['client_gender'] == 'Мужчина']['agree_binary']
    group_female = df_filtered[df_filtered['client_gender'] == 'Женщина']['agree_binary']
    t_stat_gender, p_ttest_gender = ttest_ind(group_male, group_female, equal_var=False)
    print(f"t-тест по полу клиента: t = {t_stat_gender:.4f}, p-value = {p_ttest_gender:.4f}")
    
    if p_ttest_gender < 0.05:
        print("Гипотеза подтверждается: статистически значимые различия по полу клиента.")
    else:
        print("Гипотеза не подтверждается: статистических различий по полу клиента нет.")

    # Проверка по полу оператора
    print("\nПроверка различий по полу оператора:")
    for gender in ['Мужчина', 'Женщина']:
        group = df_filtered[df_filtered['employee_gender'] == gender]['agree_binary']
        print(f"Средняя доля согласия {gender} (оператор): {group.mean():.4f}, Размер выборки: {len(group)}")

    group_male_op = df_filtered[df_filtered['employee_gender'] == 'Мужчина']['agree_binary']
    group_female_op = df_filtered[df_filtered['employee_gender'] == 'Женщина']['agree_binary']
    t_stat_op, p_ttest_op = ttest_ind(group_male_op, group_female_op, equal_var=False)
    print(f"t-тест по полу оператора: t = {t_stat_op:.4f}, p-value = {p_ttest_op:.4f}")

    if p_ttest_op < 0.05:
        print("Гипотеза подтверждается: статистически значимые различия по полу оператора.")
    else:
        print("Гипотеза не подтверждается: статистических различий по полу оператора нет.")

else:
    print("\nГипотеза по возрасту не подтверждается: статистических различий нет, проверка по полу не проводится.")
    
# Логистическая регрессия для проверки влияния всех факторов одновременно
print("\nЛогистическая регрессия для проверки влияния всех факторов одновременно:")

X = pd.get_dummies(df_filtered[['age_group', 'client_gender', 'employee_gender']], drop_first=True)
X = X.astype(float)
X = sm.add_constant(X)

y = df_filtered['agree_binary'].astype(float)

model = sm.Logit(y, X)
result = model.fit()
print(result.summary())

# Проверка взаимодействия возраста и пола клиента
print("\nПроверка взаимодействия возраста и пола клиента:")
cross_table = pd.crosstab(
    index=[df_filtered['age_group'], df_filtered['client_gender']],
    columns=df_filtered['agree_binary'],
    normalize='index'
)
print(cross_table)