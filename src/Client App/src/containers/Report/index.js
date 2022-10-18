import { Tab, Row, Col, Nav, Container, } from "react-bootstrap";
import ReportTab from "./ReportTab";

function Report() {
    return (<Container className="my-2">
        <Tab.Container id="left-tabs-example" defaultActiveKey="report">
            <Row>
                <Col>
                    <Nav variant="pills" className="flex">
                        <Nav.Item>
                            <Nav.Link eventKey="report">Report</Nav.Link>
                        </Nav.Item>
                        <Nav.Item>
                            <Nav.Link eventKey="second">Tab 2</Nav.Link>
                        </Nav.Item>
                    </Nav>
                </Col>
            </Row>

            <Row>
                <Col>
                    <Tab.Content>
                        <Tab.Pane eventKey="report">
                            {/* <Sonnet /> */}
                          <ReportTab />
                        </Tab.Pane>
                        <Tab.Pane eventKey="second">
                            {/* <Sonnet /> */}
                            efghi
                        </Tab.Pane>
                    </Tab.Content>
                </Col>
            </Row>
        </Tab.Container>
    </Container>);
}

export default Report;