import { Table, Button } from "react-bootstrap";

const ScoreItem = ({ s }) => {
    const calculate = () => {
        if (s < 40)
            return "danger";
        if (s < 70)
            return "warning";
        return "success"
    }
    return
};

function ReportTab() {
    const sampleData = {
        "data": "daily",
        "from": "2022-01-05",
        "to": "2022-01-12",
        "gap": "1",
        "sites": [
            {
                "site": "Nayay",
                "score": [
                    20, 70, 60, 90, 40, 90, 50, 85
                ]
            }, {
                "site": "fsdfdsf",
                "score": [
                    80, 70, 60, 90, 40, 30, 80, 85
                ]
            }]
    }
    const months = ["Jan", "Feb", "Mar", "Apr", "May", "June", "July",
        "Aug", "Sep", "Oct", "Nov", "Dec"];
    const generateTableHeaders = () => {
        let generated = [];
        let end = new Date(sampleData.to).getTime()
        let start = new Date(sampleData.from).getTime();
        while (start <= end) {
            let day = new Date(start).getDate();
            let month = new Date(start).getMonth();
            let endTime = start + ((sampleData.gap - 1) * 60 * 60 * 24 * 1000)
            let endDay = new Date(endTime);
            let endMonth = new Date(endTime).getMonth();

            if (parseInt(sampleData.gap) === 1)
                generated.push(day + ' ' + months[month]);
            else generated.push(day + ' ' + months[month] + " to " + endDay + ' ' + months[endMonth])
            start = start + 60 * 60 * 24 * 1000;
            console.log(start)
        }
        return generated;
    }
    const generateAverage = (scores) => {
        return [scores.reduce((pv,nv) => pv+nv,0)/scores.length];
    }

    return (<>This is an example of Report page<br />
        <div>
            <p>Daily Report</p>
            <Table >
                <thead>
                    <tr>
                        <th>Site Name</th>
                        <th>Average Score</th>
                        {generateTableHeaders().map(i => <th key={i}>{i}</th>)}
                    </tr>
                </thead>
                <tbody>
                    {sampleData.sites.map((siteData) => <tr>
                        <td className="siteName">{siteData.site}</td>
                        {generateAverage(siteData.score).map((s,i) =>
                            <td key={i} className="px-2">
                                <Button variant={s < 40 ? "danger" : s < 70 ? "warning" : "success"} className="w-100 h-100 m-0">
                                    {s}
                                </Button>
                            </td>)}
                        {siteData.score.map((s, i) => <td key={i} className="p-1"><Button variant={s < 40 ? "danger" : s < 70 ? "warning" : "success"} className="w-100 h-100 m-0" >{s}</Button></td>)}
                    </tr>)}
                </tbody>
            </Table>
        </div>
    </>);
}

export default ReportTab;